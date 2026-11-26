using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sanmon.SurvivorDemo
{
    public sealed partial class SurvivorArena
    {
        private readonly List<Material> _materials = new();
        private readonly Dictionary<Color, Material> _materialCache = new();
        private readonly Dictionary<string, Sprite> _presetIcons = new();
        private readonly List<Vector3> _obstacles = new();
        private MaterialPropertyBlock _flash;
        private Material _surfacePrototype;
        private Material _effectMaterial;
        private readonly List<RingEffect> _effects = new();
        private readonly Stack<RingEffect> _effectPool = new();
        private readonly List<ParticleEffect> _particleEffects = new();
        private readonly Stack<ParticleEffect> _particlePool = new();
        private readonly List<AudioClip> _tones = new();
        private Camera _camera;
        private bool _ownsCamera;
        private Vector3 _cameraVelocity;
        private Vector3 _cameraFocus;
        private Vector3 _cameraOffset;
        private float _shake;
        private AudioSource _audio;
        private AudioListener _addedListener;
        private float _lastSoundAt = -1;
        private CameraState _cameraState;
        private AmbientMode _oldAmbientMode;
        private Color _oldAmbient;
        private Light _keyLight;
        private Color _oldLightColor;
        private float _oldLightIntensity;
        private Quaternion _oldLightRotation;

        private void BuildWorld()
        {
            _flash = new MaterialPropertyBlock();
            _surfacePrototype = Resources.Load<Material>("SurvivorDemo/Surface");
            if (_surfacePrototype == null) throw new System.InvalidOperationException("Missing SurvivorDemo/Surface material resource.");
            _world = new GameObject("Survivor Runtime World").transform;
            _world.SetParent(transform, false);
            Shape("Surround", PrimitiveType.Cube, _world, new Vector3(0, -0.35f, 0), new Vector3(130, 0.3f, 130), SurvivorPalette.Shadow);
            Shape("Arena Floor", PrimitiveType.Cube, _world, new Vector3(0, -0.1f, 0), new Vector3(58, 0.2f, 58), SurvivorPalette.Floor);
            for (var coordinate = -24; coordinate <= 24; coordinate += 4)
            {
                Shape("Grid X", PrimitiveType.Cube, _world, new Vector3(coordinate, 0.006f, 0), new Vector3(0.035f, 0.012f, 56), SurvivorPalette.Grid);
                Shape("Grid Z", PrimitiveType.Cube, _world, new Vector3(0, 0.006f, coordinate), new Vector3(56, 0.012f, 0.035f), SurvivorPalette.Grid);
            }
            for (var axis = -1; axis <= 1; axis += 2)
            {
                Shape("Arena Edge", PrimitiveType.Cube, _world, new Vector3(axis * 28, 0.35f, 0), new Vector3(0.5f, 0.7f, 56), SurvivorPalette.Stone);
                Shape("Arena Edge", PrimitiveType.Cube, _world, new Vector3(0, 0.35f, axis * 28), new Vector3(56, 0.7f, 0.5f), SurvivorPalette.Stone);
            }
            foreach (var point in new[] { new Vector3(-9, 0, -8), new Vector3(10, 0, -8), new Vector3(-10, 0, 10), new Vector3(10, 0, 10) })
            {
                _obstacles.Add(point);
                Shape("Obstacle", PrimitiveType.Cylinder, _world, point + Vector3.up * 0.7f, new Vector3(2, 0.7f, 2), SurvivorPalette.Stone);
            }
            _player = new GameObject("Player Capsule").transform;
            _player.SetParent(_world, false);
            _playerBody = Shape("Player", PrimitiveType.Capsule, _player, Vector3.up * 1.05f, new Vector3(0.85f, 1, 0.85f), SurvivorPalette.Player).transform;
            Shape("Player Marker", PrimitiveType.Cylinder, _player, Vector3.up * 0.035f, new Vector3(1.25f, 0.025f, 1.25f), SurvivorPalette.Shadow);
            ConfigureOrbiters();

            _camera = Camera.main;
            _ownsCamera = _camera == null;
            if (_ownsCamera)
            {
                var cameraObject = new GameObject("Survivor Camera") { tag = "MainCamera" };
                cameraObject.transform.SetParent(_world, false);
                _camera = cameraObject.AddComponent<Camera>();
            }
            _cameraState = new CameraState(_camera);
            _cameraFocus = PlayerPosition + Vector3.up * 0.75f;
            _cameraOffset = _camera.transform.position - _cameraFocus;
            ApplyCameraPosition(Vector3.zero);
            if (FindObjectOfType<AudioListener>() == null) _addedListener = _camera.gameObject.AddComponent<AudioListener>();

            foreach (var light in FindObjectsOfType<Light>()) if (light.type == LightType.Directional) { _keyLight = light; break; }
            if (_keyLight == null)
            {
                var lightObject = new GameObject("Survivor Key Light");
                lightObject.transform.SetParent(_world, false);
                _keyLight = lightObject.AddComponent<Light>();
                _keyLight.type = LightType.Directional;
            }
            _oldLightColor = _keyLight.color;
            _oldLightIntensity = _keyLight.intensity;
            _oldLightRotation = _keyLight.transform.rotation;
            _keyLight.color = SurvivorPalette.Light;
            _keyLight.intensity = 1.15f;
            _keyLight.transform.rotation = Quaternion.Euler(48, -30, 0);
            _oldAmbientMode = RenderSettings.ambientMode;
            _oldAmbient = RenderSettings.ambientLight;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = SurvivorPalette.Stone;
            _audio = _world.gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0;
            _audio.volume = 0.18f;
            for (var i = 0; i < 5; i++) _tones.Add(CreateTone(i));
        }

        private Renderer Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            var shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            var collider = shape.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            var renderer = shape.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor(color);
            return renderer;
        }

        private Material MaterialFor(Color color)
        {
            if (_materialCache.TryGetValue(color, out var material)) return material;
            material = new Material(_surfacePrototype) { name = "Survivor " + ColorUtility.ToHtmlStringRGB(color), color = color };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.12f);
            _materials.Add(material);
            _materialCache[color] = material;
            return material;
        }

        private Enemy CreateEnemy(EnemyKind kind)
        {
            var root = new GameObject(kind.ToString()).transform;
            root.SetParent(_world, false);
            var tank = kind == EnemyKind.Tank;
            var color = tank ? SurvivorPalette.Tank : kind == EnemyKind.Runner ? SurvivorPalette.Gold : SurvivorPalette.Enemy;
            var body = Shape("Body", tank ? PrimitiveType.Cube : PrimitiveType.Capsule, root, Vector3.up * (tank ? 0.9f : 0.8f), tank ? new Vector3(1.5f, 1.8f, 1.5f) : new Vector3(0.7f, 0.8f, 0.7f), color);
            var healthBar = new GameObject("Health Bar").transform;
            healthBar.SetParent(root, false);
            healthBar.localPosition = Vector3.up * 2.15f;
            healthBar.rotation = _camera.transform.rotation;
            Shape("Health Background", PrimitiveType.Cube, healthBar, Vector3.zero, new Vector3(0.9f, 0.09f, 0.1f), SurvivorPalette.Shadow);
            var bar = Shape("Health", PrimitiveType.Cube, healthBar, new Vector3(0, 0, -0.06f), new Vector3(0.85f, 0.07f, 0.08f), SurvivorPalette.Player).transform;
            return new Enemy { root = root, body = body, healthBar = healthBar, bar = bar, kind = kind, radius = tank ? 0.85f : 0.45f };
        }

        private void ConfigureOrbiters()
        {
            var stats = Parameters(SkillKind.Orbit);
            var previousCount = _orbiters.Length;
            if (previousCount < stats.orbiterCount)
            {
                System.Array.Resize(ref _orbiters, stats.orbiterCount);
                for (var i = previousCount; i < _orbiters.Length; i++)
                    _orbiters[i] = Shape("Orbit Blade", PrimitiveType.Cube, _world, Vector3.zero, new Vector3(0.25f, 0.18f, 0.65f), stats.color).transform;
            }
            for (var i = 0; i < _orbiters.Length; i++)
            {
                _orbiters[i].GetComponent<Renderer>().sharedMaterial = MaterialFor(stats.color);
                _orbiters[i].gameObject.SetActive(AutoAttack && SkillLevel(SkillKind.Orbit) > 0 && i < stats.orbiterCount);
                if (i < stats.orbiterCount)
                {
                    var angle = Stats.Elapsed * stats.orbitSpeed + i * Mathf.PI * 2 / stats.orbiterCount;
                    _orbiters[i].position = PlayerPosition + new Vector3(Mathf.Cos(angle) * stats.radius, stats.orbitHeight, Mathf.Sin(angle) * stats.radius);
                }
            }
        }

        private Projectile CreateProjectile()
        {
            var body = Shape("Bolt", PrimitiveType.Sphere, _world, Vector3.zero, new Vector3(0.22f, 0.22f, 0.55f), Parameters(SkillKind.Bolt).color);
            return new Projectile { root = body.transform, body = body };
        }
        private Gem CreateGem()
        {
            var root = Shape("XP", PrimitiveType.Cube, _world, Vector3.zero, Vector3.one * 0.25f, SurvivorPalette.Player).transform;
            root.localRotation = Quaternion.Euler(35, 45, 20);
            return new Gem { root = root };
        }

        private Vector3 Constrain(Vector3 position, float radius)
        {
            position.x = Mathf.Clamp(position.x, -ArenaRadius + radius, ArenaRadius - radius);
            position.z = Mathf.Clamp(position.z, -ArenaRadius + radius, ArenaRadius - radius);
            position.y = 0;
            foreach (var obstacle in _obstacles)
            {
                var delta = position - obstacle;
                var distance = radius + 1.05f;
                if (delta.sqrMagnitude >= distance * distance) continue;
                position = obstacle + (delta.sqrMagnitude < 0.0001f ? Vector3.right : delta.normalized) * distance;
            }
            return position;
        }

        private Vector2 CameraRelativeMovement(Vector2 input)
        {
            if (_camera == null) return input;
            var right = Vector3.ProjectOnPlane(_camera.transform.right, Vector3.up).normalized;
            var forward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
            var direction = right * input.x + forward * input.y;
            return new Vector2(direction.x, direction.z);
        }

        private void ApplyCameraPosition(Vector3 shakeOffset)
        {
            var position = _camera.transform.position;
            var forward = _camera.transform.forward;
            // 用 MainCamera 当前高度和朝向求 X/Z，保留 Inspector 中的其它设置。
            var offset = _cameraOffset;
            if (forward.y < -0.001f)
                offset = -forward * ((position.y - _cameraFocus.y) / -forward.y);
            position.x = _cameraFocus.x + offset.x + shakeOffset.x;
            position.z = _cameraFocus.z + offset.z + shakeOffset.z;
            _camera.transform.position = position;
        }

        private void LateUpdate()
        {
            if (!_initialized || _camera == null) return;
            var target = PlayerPosition + Vector3.up * 0.75f;
            _cameraFocus = Vector3.SmoothDamp(_cameraFocus, target, ref _cameraVelocity, 0.18f, 100, Time.unscaledDeltaTime);
            _shake = Mathf.MoveTowards(_shake, 0, Time.unscaledDeltaTime);
            var offset = new Vector3(Mathf.Sin(Time.unscaledTime * 83), 0, Mathf.Cos(Time.unscaledTime * 71)) * _shake;
            ApplyCameraPosition(offset);
            // 对齐屏幕平面，暂停或调整镜头时也保持血条水平。
            var rotation = _camera.transform.rotation;
            foreach (var enemy in _enemies) enemy.healthBar.rotation = rotation;
        }

        private Material EffectMaterial()
        {
            if (_effectMaterial != null) return _effectMaterial;
            var shader = Shader.Find("Sprites/Default");
            _effectMaterial = new Material(shader != null ? shader : _surfacePrototype.shader) { name = "Survivor Effects", color = Color.white };
            _materials.Add(_effectMaterial);
            return _effectMaterial;
        }

        private RingEffect Burst(Vector3 position, float radius, Color color, float duration = 0.45f, bool persistent = false)
        {
            if (_effects.Count >= 64) return null;
            RingEffect effect;
            if (_effectPool.Count > 0) effect = _effectPool.Pop();
            else
            {
                var root = new GameObject("Skill Ring");
                root.transform.SetParent(_world, false);
                var line = root.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = 48;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.alignment = LineAlignment.View;
                for (var i = 0; i < 48; i++) { var angle = i * Mathf.PI * 2 / 48; line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle))); }
                effect = new RingEffect { line = line };
            }
            effect.line.sharedMaterial = EffectMaterial();
            effect.line.transform.position = position + Vector3.up * 0.12f;
            effect.line.transform.localScale = persistent ? new Vector3(radius, 1, radius) : Vector3.one * 0.1f;
            effect.line.startColor = effect.line.endColor = color;
            effect.line.startWidth = effect.line.endWidth = persistent ? 0.08f : 0.24f;
            effect.line.gameObject.SetActive(true);
            effect.life = effect.duration = duration;
            effect.expiresAt = Stats.Elapsed + duration;
            effect.radius = radius;
            effect.color = color;
            effect.persistent = persistent;
            _effects.Add(effect);
            return effect;
        }

        private void RecycleRing(RingEffect effect)
        {
            if (effect == null || !_effects.Remove(effect)) return;
            effect.line.gameObject.SetActive(false);
            _effectPool.Push(effect);
        }

        private void EmitSkillParticles(Vector3 position, float radius, Color color, bool frost)
        {
            if (_particleEffects.Count >= 12) return;
            var effect = _particlePool.Count > 0 ? _particlePool.Pop() : CreateParticleEffect();
            var system = effect.system;
            system.gameObject.SetActive(true);
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.gravityModifier = frost ? 0.12f : 0.55f;
            system.Play();
            for (var i = 0; i < 48; i++)
            {
                var angle = i * 2.399963f + Stats.Elapsed;
                var spread = (i % 11) / 10f;
                var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var particle = new ParticleSystem.EmitParams
                {
                    position = position + Vector3.up * 0.18f + radial * radius * spread * (frost ? 0.9f : 0.25f),
                    velocity = radial * (frost ? 0.8f + spread : 3 + spread * 5) + Vector3.up * (frost ? 1.5f : 2.5f),
                    startColor = color,
                    startLifetime = 0.4f + spread * 0.4f,
                    startSize = frost ? 0.08f + spread * 0.14f : 0.16f + spread * 0.2f,
                    rotation = angle * Mathf.Rad2Deg
                };
                system.Emit(particle, 1);
            }
            effect.life = 1;
            _particleEffects.Add(effect);
        }

        private ParticleEffect CreateParticleEffect()
        {
            var root = new GameObject("Skill Fragments");
            root.transform.SetParent(_world, false);
            var system = root.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1;
            main.maxParticles = 64;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            var colors = system.colorOverLifetime;
            colors.enabled = true;
            colors.color = new ParticleSystem.MinMaxGradient(fade);
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = EffectMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return new ParticleEffect { system = system };
        }

        private void TickEffects(float dt)
        {
            for (var i = _effects.Count - 1; i >= 0; i--)
            {
                var effect = _effects[i];
                effect.life = effect.expiresAt - Stats.Elapsed;
                var progress = Mathf.Clamp01(1 - effect.life / effect.duration);
                var radius = effect.persistent ? effect.radius : Mathf.Lerp(0.15f, effect.radius, 1 - Mathf.Pow(1 - progress, 3));
                effect.line.transform.localScale = new Vector3(radius, 1, radius);
                effect.line.startWidth = effect.line.endWidth = effect.persistent ? 0.08f + Mathf.Sin(Stats.Elapsed * 5) * 0.015f : Mathf.Lerp(0.24f, 0.005f, progress);
                var fade = effect.persistent ? Mathf.Clamp01((effect.duration - effect.life) / 0.2f) * Mathf.Clamp01(effect.life / 0.4f) : 1 - progress;
                effect.line.startColor = effect.line.endColor = SurvivorPalette.Alpha(effect.color, effect.color.a * fade);
                if (effect.life > 0) continue;
                effect.line.gameObject.SetActive(false);
                _effectPool.Push(effect);
                _effects.RemoveAt(i);
            }
            for (var i = _particleEffects.Count - 1; i >= 0; i--)
            {
                var effect = _particleEffects[i];
                effect.life -= dt;
                if (effect.life > 0) continue;
                effect.system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                effect.system.gameObject.SetActive(false);
                _particlePool.Push(effect);
                _particleEffects.RemoveAt(i);
            }
        }

        private void ClearEffects()
        {
            foreach (var effect in _effects) { effect.line.gameObject.SetActive(false); _effectPool.Push(effect); }
            _effects.Clear();
            foreach (var effect in _particleEffects)
            {
                effect.system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                effect.system.gameObject.SetActive(false);
                _particlePool.Push(effect);
            }
            _particleEffects.Clear();
            _frostZones.Clear();
        }

        private static AudioClip CreateTone(int kind)
        {
            const int rate = 22050;
            var duration = kind == 2 ? 0.3f : 0.09f;
            var samples = new float[Mathf.CeilToInt(rate * duration)];
            var frequencies = new[] { 660f, 160f, 880f, 260f, 1200f };
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)rate;
                var pitch = frequencies[kind] * (kind == 2 ? 1 + Mathf.Floor(time * 12) * 0.25f : 1 - time * 2);
                samples[i] = Mathf.Sin(time * pitch * Mathf.PI * 2) * Mathf.Pow(1 - time / duration, 2) * 0.5f;
            }
            var clip = AudioClip.Create("Survivor tone " + kind, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlayTone(int kind)
        {
            if (_audio == null || Time.unscaledTime - _lastSoundAt < 0.045f) return;
            _lastSoundAt = Time.unscaledTime;
            _audio.PlayOneShot(_tones[kind]);
        }

        internal Sprite PresetIcon(string key)
        {
            if (_presetIcons.TryGetValue(key, out var icon)) return icon;
            var color = System.Enum.TryParse(key, out SkillKind skill) ? _skills.Resolve(skill, 0).color : key == "Tank" ? SurvivorPalette.Tank : key == "Grunt" ? SurvivorPalette.Enemy : SurvivorPalette.Gold;
            var texture = new Texture2D(12, 12, TextureFormat.RGBA32, false) { name = "Preset " + key, filterMode = FilterMode.Point };
            for (var y = 0; y < 12; y++) for (var x = 0; x < 12; x++) texture.SetPixel(x, y, x < 2 || y < 2 || x > 9 || y > 9 ? SurvivorPalette.Shadow : color);
            texture.Apply();
            icon = Sprite.Create(texture, new Rect(0, 0, 12, 12), Vector2.one * 0.5f, 12);
            _presetIcons[key] = icon;
            return icon;
        }

        private void ReleaseWorld()
        {
            if (_addedListener != null) Destroy(_addedListener);
            if (_camera != null && !_ownsCamera) _cameraState.Restore(_camera);
            if (_keyLight != null) { _keyLight.color = _oldLightColor; _keyLight.intensity = _oldLightIntensity; _keyLight.transform.rotation = _oldLightRotation; }
            RenderSettings.ambientMode = _oldAmbientMode;
            RenderSettings.ambientLight = _oldAmbient;
            if (_world != null) Destroy(_world.gameObject);
            foreach (var material in _materials) Destroy(material);
            foreach (var clip in _tones) Destroy(clip);
            foreach (var icon in _presetIcons.Values) { Destroy(icon.texture); Destroy(icon); }
        }

        private sealed class RingEffect { public LineRenderer line; public float life, duration, radius, expiresAt; public Color color; public bool persistent; }
        private sealed class ParticleEffect { public ParticleSystem system; public float life; }
        private readonly struct CameraState
        {
            private readonly Vector2 position;
            public CameraState(Camera camera) { var original = camera.transform.position; position = new Vector2(original.x, original.z); }
            public void Restore(Camera camera) { var current = camera.transform.position; current.x = position.x; current.z = position.y; camera.transform.position = current; }
        }
    }
}
