using System;
using System.Collections.Generic;
using System.Threading;
using GameConsole;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    /// <summary> 纯代码、集中更新的俯视角生存试验场。无预制体或场景烘焙依赖。 </summary>
    public sealed partial class SurvivorArena : MonoBehaviour
    {
        public static SurvivorArena Instance { get; private set; }
        public const float ArenaRadius = 26;
        public int EnemyLimit => _waves.enemyLimit;
        public float RunDuration => _waves.runDuration;
        public SurvivorStats Stats { get; private set; } = new();
        public GameDevToolsController Tools { get; private set; }
        public bool IsPaused { get; private set; }
        public bool HasEnded { get; private set; }
        public bool HasWon { get; private set; }
        public int PendingModifications { get; private set; }
        public int PendingSkills { get; private set; }
        public bool IsChoosingReward => _rewardChoices.Count > 0 && !HasEnded;
        public bool IsChoosingSkill => IsChoosingReward && _rewardChoices[0].kind == SurvivorRewardKind.Skill;
        public SurvivorSkillCatalog SkillCatalog => _skills;
        public SurvivorWaveCatalog WaveCatalog => _waves;
        public SurvivorEnemyCatalog EnemyCatalog => _enemyCatalog;
        public SurvivorLevelCatalog LevelCatalog => _levels;
        public SurvivorEnemySkillCatalog EnemySkillCatalog => _enemySkills;
        public bool AutoSpawn { get; set; } = true;
        public bool AutoAttack { get; set; } = true;
        public int EnemyCount => _enemies.Count;
        public int ProjectileCount => _projectiles.Count;
        public int GemCount => _gems.Count;
        public Vector3 PlayerPosition => _player != null ? _player.position : Vector3.zero;
        public CancellationToken Lifetime => _lifetime.Token;
        public float GameSpeed { get => _gameSpeed; set { _gameSpeed = FiniteClamp(value, 0.25f, 3); ApplyTimeScale(); } }
        public float SfxVolume { get => _audio != null ? _audio.volume : 0; set { if (_audio != null) _audio.volume = FiniteClamp(value, 0, 1); } }

        private readonly List<Enemy> _enemies = new();
        private readonly Dictionary<string, Stack<Enemy>> _enemyPool = new(StringComparer.Ordinal);
        private readonly List<Projectile> _projectiles = new();
        private readonly Stack<Projectile> _projectilePool = new();
        private readonly List<Gem> _gems = new();
        private readonly Stack<Gem> _gemPool = new();
        private readonly List<FrostZone> _frostZones = new();
        private readonly int[] _skillLevels = new int[SurvivorSkills.SKILL_COUNT];
        private readonly int[] _skillModifications = new int[SurvivorSkills.SKILL_COUNT];
        private readonly float[] _readyAt = new float[SurvivorSkills.SKILL_COUNT];
        private readonly CancellationTokenSource _lifetime = new();
        private System.Random _random;
        private SurvivorSkillCatalog _skills;
        private SurvivorWaveCatalog _waves;
        private SurvivorEnemyCatalog _enemyCatalog;
        private SurvivorLevelCatalog _levels;
        private SurvivorEnemySkillCatalog _enemySkills;
        private bool _initialized;
        private float _originalTimeScale;
        private float _gameSpeed = 1;
        private float _nextSpawn;
        private bool _hasAutoSpawned;
        private float _boltFollowUpAt = -1;
        private bool _boltFollowUpBurst;
        private Vector3 _boltFollowUpDirection;
        private int _boltVolleysRemaining;
        private float _invulnerableUntil;
        private float _dashUntil;
        private float _dashReadyAt;
        private float _moveOverrideRemaining;
        private Vector2 _moveOverride;
        private Vector3 _facing = Vector3.forward;
        private Vector3 _dashDirection;
        private int _bestKills;
        private Transform _world;
        private Transform _player;
        private Transform _playerBody;
        private Font _registeredFont;
        private Transform[] _orbiters = Array.Empty<Transform>();

        public void Initialize(GameDevToolsController tools, Font font = null, SurvivorConfiguration configuration = null)
        {
            if (_initialized) return;
            if (Instance != null && Instance != this) throw new InvalidOperationException("Only one SurvivorArena can run at a time.");
            SetConfiguration(configuration ?? SurvivorConfiguration.Load());
            _initialized = true;
            Instance = this;
            Tools = tools;
            _registeredFont = font;
            _originalTimeScale = Time.timeScale;
            _bestKills = PlayerPrefs.GetInt("GameDevTools.Survivor.BestKills", 0);
            BuildWorld();
            BuildHud(font);
            ResetRun(42);
            if (Tools != null)
            {
                Tools.Macros?.RequestCommands();
                SurvivorCommands.RemoveLegacyExampleMacros();
                Tools.Print("Survivor 开发工具已就绪。SurvivorHelp() 查看调试命令；F1 打开工具，Esc 暂停。", LogType.Log);
            }
        }

        public void RestartRun(int seed = 42)
        {
            if (!_initialized) throw new InvalidOperationException("Arena has not been initialized.");
            if (!SurvivorConfiguration.UsesWebRequest) SetConfiguration(SurvivorConfiguration.Load());
            ResetRun(seed);
        }

        private void ResetRun(int seed)
        {
            _audio.Stop();
            _audio.UnPause();
            ClearEnemies(false);
            foreach (var projectile in _projectiles) { projectile.root.gameObject.SetActive(false); _projectilePool.Push(projectile); }
            _projectiles.Clear();
            foreach (var gem in _gems) { gem.root.gameObject.SetActive(false); _gemPool.Push(gem); }
            _gems.Clear();
            ClearEffects();
            _damageZones.Clear();
            ClearEnemyProjectiles();
            var initialHealth = _levels.MaxHealthAtLevel(1);
            Stats = new SurvivorStats
            {
                Health = initialHealth,
                MaxHealth = initialHealth,
                HealthRegenerationPerSecond = _levels.HealthRegenerationAtLevel(1),
                BaseDamage = _levels.BaseDamageAtLevel(1)
            };
            _random = new System.Random(seed);
            _player.position = Vector3.zero;
            _facing = Vector3.forward;
            IsPaused = HasEnded = HasWon = false;
            AutoSpawn = AutoAttack = true;
            _gameSpeed = 1;
            _nextSpawn = _waves.firstSpawnDelay;
            _hasAutoSpawned = false;
            _boltFollowUpAt = -1;
            _boltVolleysRemaining = 0;
            _invulnerableUntil = _dashUntil = _dashReadyAt = 0;
            _moveOverrideRemaining = 0;
            ResetProgression();
            ConfigureOrbiters();
            ApplyTimeScale();
            SpawnGroups(_waves.initialSpawns);
            SpawnGroups(_waves.waves[0].entrySpawns);
            Toast("试验开始  /  SURVIVE " + TimeSpan.FromSeconds(RunDuration).ToString(@"mm\:ss"));
            RefreshHud(true);
        }

        private void Update()
        {
            if (!_initialized) return;
            if (Input.GetKeyDown(KeyCode.Escape) && !IsChoosingReward) SetPaused(!IsPaused);
            // F1 已由工具配置占用时交给总控处理，避免同一按键开关两次。
            if (Input.GetKeyDown(KeyCode.F1) && Config.KeyCode != KeyCode.F1) ToggleTools();
            var movement = Vector2.zero;
            if (!IsPaused && !IsChoosingReward && !HasEnded && (Tools == null || !Tools.IsOpen))
            {
                movement = CameraRelativeMovement(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")));
                if (Input.GetKeyDown(KeyCode.Space)) Dash();
            }

            TickSimulation(Mathf.Min(Time.deltaTime, 0.1f), movement);
            RefreshHud(false);
        }

        public void TickSimulation(float deltaTime, Vector2 movement)
        {
            if (!_initialized || IsPaused || IsChoosingReward || HasEnded || deltaTime <= 0) return;
            deltaTime = FiniteClamp(deltaTime, 0, 0.1f);
            if (!IsFinite(movement.x) || !IsFinite(movement.y)) throw new ArgumentException("Movement must be finite.");
            Stats.Elapsed += deltaTime;
            if (Stats.Health > 0 && Stats.Health < Stats.MaxHealth && IsFinite(Stats.HealthRegenerationPerSecond) && Stats.HealthRegenerationPerSecond > 0)
                Stats.Health = Mathf.Min(Stats.MaxHealth, Stats.Health + Stats.HealthRegenerationPerSecond * deltaTime);
            if (_moveOverrideRemaining > 0) { movement = _moveOverride; _moveOverrideRemaining -= deltaTime; }
            var direction = new Vector3(movement.x, 0, movement.y);
            direction = Vector3.ClampMagnitude(direction, 1);
            if (direction.sqrMagnitude > 0.001f) _facing = direction.normalized;
            var dashing = Stats.Elapsed < _dashUntil;
            var dashMultiplier = Parameters(SkillKind.Dash).dashSpeedMultiplier;
            _player.position = Constrain(PlayerPosition + (dashing ? _dashDirection * dashMultiplier : direction) * (Stats.MoveSpeed * deltaTime), 0.5f);
            _playerBody.localRotation = Quaternion.Slerp(_playerBody.localRotation, Quaternion.LookRotation(_facing), 1 - Mathf.Exp(-16 * deltaTime));
            _playerBody.localPosition = new Vector3(0, 1.05f + Mathf.Sin(Stats.Elapsed * (direction.sqrMagnitude > 0 ? 14 : 3)) * 0.055f, 0);
            _playerBody.localScale = new Vector3(0.85f, dashing ? 0.8f : 1, 0.85f);

            var waveIndex = _waves.IndexAt(Stats.Elapsed);
            var wave = waveIndex + 1;
            if (wave != Stats.Wave)
            {
                Stats.Wave = wave;
                SpawnGroups(_waves.waves[waveIndex].entrySpawns);
                Toast("第 " + wave + " 波  /  " + _waves.waves[waveIndex].displayName);
                Debug.Log("[Survivor] Wave " + wave);
            }

            if (AutoSpawn && Stats.Elapsed >= _nextSpawn)
            {
                var definition = _waves.waves[waveIndex];
                _hasAutoSpawned = true;
                _nextSpawn = Stats.Elapsed + definition.spawnInterval;
                var id = definition.enemyPool[_random.Next(definition.enemyPool.Length)];
                Spawn(id, definition.spawnCount);
            }

            TickBoltFollowUp();
            if (AutoAttack)
            {
                for (var i = 0; i < SurvivorSkills.COMBAT_SKILL_COUNT; i++)
                    if ((SkillKind)i != SkillKind.Orbit) TryAutoCast((SkillKind)i, AutoCastRange((SkillKind)i));
            }
            TickFrostZones();
            TickDamageZones();
            TickBleeding();
            TickEnemies(deltaTime);
            if (HasEnded) return;
            TickEnemyProjectiles(deltaTime);
            if (HasEnded) return;
            TickProjectiles(deltaTime);
            TickGems(deltaTime);
            if (IsChoosingReward) return;
            TickOrbiters();
            TickEffects(deltaTime);
            if (Stats.Elapsed >= RunDuration) EndRun(true);
        }

        public void SetPaused(bool paused)
        {
            if (HasEnded) return;
            IsPaused = paused;
            ApplyTimeScale();
            RefreshHud(true);
        }

        public void SetHealth(float health)
        {
            Stats.Health = FiniteClamp(health, 0, Stats.MaxHealth);
            if (Stats.Health <= 0) EndRun(false);
            RefreshHud(true);
        }

        public void Revive()
        {
            Stats.Health = Stats.MaxHealth;
            HasEnded = HasWon = IsPaused = false;
            _invulnerableUntil = Stats.Elapsed + 3;
            ApplyTimeScale();
            Burst(PlayerPosition, 4, SurvivorPalette.Player);
            Toast("复活 / 3 秒保护");
        }

        public void HurtPlayer(float damage)
        {
            damage = FiniteClamp(damage, 0, 10000);
            if (HasEnded || Stats.GodMode || Stats.Elapsed < _invulnerableUntil) return;
            _invulnerableUntil = Stats.Elapsed + 0.65f;
            _shake = 0.22f;
            PlayTone(1);
            SetHealth(Stats.Health - damage);
        }

        public void Teleport(ArenaPoint point)
        {
            if (!IsFinite(point.x) || !IsFinite(point.z)) throw new ArgumentException("Position must be finite.");
            Burst(PlayerPosition, 2, SurvivorPalette.Player);
            _player.position = Constrain(point.Position, 0.5f);
            Burst(PlayerPosition, 2, SurvivorPalette.Player);
        }

        public void MoveFor(Vector2 direction, float seconds)
        {
            if (!IsFinite(direction.x) || !IsFinite(direction.y)) throw new ArgumentException("Direction must be finite.");
            _moveOverride = Vector2.ClampMagnitude(direction, 1);
            _moveOverrideRemaining = FiniteClamp(seconds, 0, 10);
        }

        public bool Dash()
        {
            if (IsPaused || IsChoosingReward || HasEnded || Stats.Elapsed < _dashReadyAt) return false;
            var stats = Parameters(SkillKind.Dash);
            _dashDirection = _facing;
            _dashUntil = Stats.Elapsed + stats.duration;
            _dashReadyAt = Stats.Elapsed + stats.cooldown;
            _invulnerableUntil = Mathf.Max(_invulnerableUntil, Stats.Elapsed + stats.invulnerabilityDuration);
            DamageArea(PlayerPosition, stats, SkillKind.Dash);
            if (stats.healing > 0) SetHealth(Mathf.Min(Stats.MaxHealth, Stats.Health + stats.healing));
            Burst(PlayerPosition, stats.radius, stats.color);
            PlayTone(0);
            return true;
        }

        private void TryAutoCast(SkillKind skill, float radius)
        {
            if (SkillLevel(skill) == 0 || SkillCooldown(skill) > 0) return;
            foreach (var enemy in _enemies)
            {
                if ((enemy.root.position - PlayerPosition).sqrMagnitude > radius * radius) continue;
                if (skill == SkillKind.Bolt)
                {
                    FireBolts(false);
                    _readyAt[(int)skill] = Stats.Elapsed + SkillInterval(skill);
                }
                else TryCast(skill);
                return;
            }
        }

        public bool TryCast(SkillKind skill, bool force = false)
        {
            if (skill == SkillKind.Dash) return Dash();
            var level = SkillLevel(skill);
            if (level == 0 || (!force && (IsPaused || IsChoosingReward || HasEnded || SkillCooldown(skill) > 0))) return false;
            if (skill == SkillKind.Bolt) FireBolts(true);
            else if (skill == SkillKind.Orbit) Burst(PlayerPosition, Parameters(skill).radius, Parameters(skill).color);
            else if (skill == SkillKind.Nova) CastShockwave();
            else if (skill == SkillKind.Frost && !CastFrostZone()) return false;
            else if (skill == SkillKind.Lightning && !CastLightning()) return false;
            else if (skill == SkillKind.Flame && !CastDamageZone(skill, true)) return false;
            else if (skill == SkillKind.Missile && !FireMissiles()) return false;
            else if (skill == SkillKind.Venom && !CastDamageZone(skill, false)) return false;
            _readyAt[(int)skill] = Stats.Elapsed + SkillInterval(skill);
            return true;
        }

        private void CastShockwave()
        {
            var stats = Parameters(SkillKind.Nova);
            var origin = PlayerPosition;
            var radius = stats.radius;
            for (var i = _enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemies[i];
                var away = enemy.root.position - origin;
                if (away.sqrMagnitude > radius * radius) continue;
                var direction = away.sqrMagnitude > 0.001f ? away.normalized : _facing;
                enemy.knockbackVelocity = direction * stats.knockback * (_enemyCatalog.Get(enemy.id).heavy ? stats.heavyKnockbackMultiplier : 1);
                enemy.knockbackDecay = stats.knockbackDecay;
                ApplyDebuffs(enemy, stats.debuffs, SkillKind.Nova);
                DamageEnemy(i, stats.damageMultiplier * Stats.BaseDamage);
            }
            Burst(origin, radius, stats.color, 0.5f);
            Burst(origin, radius * 0.75f, SurvivorPalette.Alpha(stats.color, 0.6f), 0.65f);
            EmitSkillParticles(origin, radius, stats.color, false);
            _shake = Mathf.Max(_shake, 0.13f);
            PlayTone(3);
        }

        private bool CastFrostZone()
        {
            if (_frostZones.Count >= 8) return false;
            var stats = Parameters(SkillKind.Frost);
            var origin = PlayerPosition;
            var radius = stats.radius;
            var duration = stats.duration;
            var zone = new FrostZone
            {
                position = origin, radius = radius, createdAt = Stats.Elapsed, expiresAt = Stats.Elapsed + duration,
                nextDamageAt = Stats.Elapsed, damageMultiplier = stats.damageMultiplier,
                tickInterval = stats.tickInterval, debuffs = stats.debuffs
            };
            Burst(origin, radius, stats.color, 0.55f);
            zone.outer = Burst(origin, radius, SurvivorPalette.Alpha(stats.color, 0.8f), duration, true);
            zone.middle = Burst(origin, radius * 0.66f, SurvivorPalette.Alpha(stats.color, 0.3f), duration, true);
            zone.inner = Burst(origin, radius * 0.33f, SurvivorPalette.Alpha(stats.color, 0.2f), duration, true);
            _frostZones.Add(zone);
            EmitSkillParticles(origin, radius, stats.color, true);
            _shake = Mathf.Max(_shake, 0.05f);
            PlayTone(0);
            return true;
        }

        private void TickFrostZones()
        {
            for (var i = _frostZones.Count - 1; i >= 0; i--)
            {
                var zone = _frostZones[i];
                if (Stats.Elapsed >= zone.expiresAt) { _frostZones.RemoveAt(i); continue; }
                var dealDamage = Stats.Elapsed >= zone.nextDamageAt;
                if (dealDamage) { zone.nextDamageAt = Stats.Elapsed + zone.tickInterval; _frostZones[i] = zone; }
                for (var j = _enemies.Count - 1; j >= 0; j--)
                {
                    var enemy = _enemies[j];
                    if ((enemy.root.position - zone.position).sqrMagnitude > zone.radius * zone.radius) continue;
                    ApplyDebuffs(enemy, zone.debuffs, SkillKind.Frost, zone.expiresAt);
                    if (dealDamage) DamageEnemy(j, zone.damageMultiplier * Stats.BaseDamage);
                }
            }
        }

        public int Spawn(EnemyKind kind, int count, ArenaPoint? location = null) => Spawn(kind.ToString(), count, location);

        public int Spawn(string id, int count, ArenaPoint? location = null)
        {
            var definition = _enemyCatalog.Get(id);
            count = Mathf.Clamp(count, 0, Mathf.Max(0, EnemyLimit - _enemies.Count));
            for (var i = 0; i < count; i++)
            {
                if (!_enemyPool.TryGetValue(id, out var pool)) _enemyPool[id] = pool = new Stack<Enemy>();
                var enemy = pool.Count > 0 ? pool.Pop() : CreateEnemy(id);
                enemy.sizeSample = (float)_random.NextDouble();
                ApplyEnemyAppearance(enemy, definition);
                var angle = (float)_random.NextDouble() * Mathf.PI * 2;
                var distance = location.HasValue ? 0.8f + i * 0.35f : Mathf.Lerp(_waves.spawnDistanceMin, _waves.spawnDistanceMax, (float)_random.NextDouble());
                var position = (location?.Position ?? PlayerPosition) + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
                enemy.root.position = Constrain(position, enemy.radius);
                enemy.spawnWave = Stats.Wave;
                enemy.health = enemy.maxHealth = definition.HealthAtWave(enemy.spawnWave);
                enemy.body.sharedMaterial = MaterialFor(definition.color);
                enemy.contactAt = enemy.orbitAt = enemy.frozenUntil = enemy.flashUntil = 0;
                enemy.bleedUntil = enemy.nextBleedAt = 0;
                enemy.slowUntil = 0;
                enemy.slowMultiplier = 1;
                enemy.knockbackDecay = Parameters(SkillKind.Nova).knockbackDecay;
                enemy.knockbackVelocity = Vector3.zero;
                SynchronizeEnemySkills(enemy, definition, null, true);
                enemy.body.SetPropertyBlock(null);
                enemy.root.gameObject.SetActive(true);
                _enemies.Add(enemy);
            }
            return count;
        }

        public int ClearEnemies(bool grantExperience = true)
        {
            var count = _enemies.Count;
            for (var i = _enemies.Count - 1; i >= 0; i--)
            {
                if (grantExperience) KillEnemy(i);
                else { var enemy = _enemies[i]; CancelEnemyCast(enemy); enemy.root.gameObject.SetActive(false); _enemyPool[enemy.id].Push(enemy); _enemies.RemoveAt(i); }
            }
            return count;
        }

        private void TickEnemies(float dt)
        {
            for (var i = _enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemies[i];
                var delta = PlayerPosition - enemy.root.position;
                var frozen = Stats.Elapsed < enemy.frozenUntil;
                var knockedBack = enemy.knockbackVelocity.sqrMagnitude > 0.1f;
                var definition = _enemyCatalog.Get(enemy.id);
                var speed = definition.moveSpeed;
                if (Stats.Elapsed < enemy.slowUntil) speed *= enemy.slowMultiplier;
                if (Stats.Elapsed < enemy.hasteUntil) speed *= enemy.hasteMultiplier;
                var velocity = EnemyMovementDirection(definition, delta);
                for (var j = Mathf.Max(0, i - 8); j < i; j++)
                {
                    var away = enemy.root.position - _enemies[j].root.position;
                    if (away.sqrMagnitude > 0.001f && away.sqrMagnitude < 1.8f) velocity += away.normalized * 0.7f;
                }
                var movement = frozen || knockedBack ? Vector3.zero : Vector3.ClampMagnitude(velocity, 1) * speed;
                if (enemy.cast != null) movement = Vector3.zero;
                if (!frozen && !knockedBack && Stats.Elapsed < enemy.chargeUntil) movement = enemy.chargeDirection * enemy.chargeSpeed;
                enemy.previousPosition = enemy.root.position;
                enemy.root.position = Constrain(enemy.root.position + (movement + enemy.knockbackVelocity) * dt, enemy.radius);
                enemy.knockbackVelocity *= Mathf.Exp(-enemy.knockbackDecay * dt);
                if (!frozen && !knockedBack && delta.sqrMagnitude > 0.01f) enemy.body.transform.rotation = Quaternion.LookRotation(delta);
                enemy.bar.localScale = new Vector3(Mathf.Max(0.01f, enemy.health / enemy.maxHealth) * 0.85f, 0.07f, 0.08f);
                if (Stats.Elapsed >= enemy.flashUntil)
                {
                    if (frozen) { _flash.SetColor("_Color", SurvivorPalette.Frost); enemy.body.SetPropertyBlock(_flash); }
                    else enemy.body.SetPropertyBlock(null);
                }
                if (frozen || knockedBack) { CancelEnemyCast(enemy); enemy.chargeUntil = 0; continue; }
                TickEnemyAbilities(enemy, definition);
                if (HasEnded) return;
                if (delta.sqrMagnitude < (enemy.radius + 0.65f) * (enemy.radius + 0.65f) && Stats.Elapsed >= enemy.contactAt)
                {
                    enemy.contactAt = Stats.Elapsed + definition.contactInterval;
                    HurtPlayer(definition.contactDamage);
                    if (HasEnded) return;
                }
            }
        }

        private void TickBoltFollowUp()
        {
            if (_boltFollowUpAt < 0 || Stats.Elapsed < _boltFollowUpAt) return;
            _boltFollowUpAt = -1;
            if (_boltVolleysRemaining <= 0 || SkillLevel(SkillKind.Bolt) == 0) return;
            _boltVolleysRemaining--;
            FireBolts(_boltFollowUpBurst, true);
            if (_boltVolleysRemaining > 0) _boltFollowUpAt = Stats.Elapsed + Parameters(SkillKind.Bolt).volleyInterval;
        }

        private void FireBolts(bool burst, bool followUp = false)
        {
            if (SkillLevel(SkillKind.Bolt) == 0 || _projectiles.Count >= 220) return;
            var stats = Parameters(SkillKind.Bolt);
            Enemy nearest = null;
            var best = stats.radius * stats.radius;
            foreach (var enemy in _enemies)
            {
                var distance = (enemy.root.position - PlayerPosition).sqrMagnitude;
                if (distance < best) { best = distance; nearest = enemy; }
            }
            if (nearest == null && !burst && !followUp) return;
            var direction = nearest != null ? (nearest.root.position - PlayerPosition).normalized : followUp ? _boltFollowUpDirection : _facing;
            var count = Mathf.Min(220 - _projectiles.Count, stats.projectileCount);
            for (var i = 0; i < count; i++)
            {
                var projectile = _projectilePool.Count > 0 ? _projectilePool.Pop() : CreateProjectile();
                projectile.direction = Quaternion.Euler(0, (i - (count - 1) / 2f) * stats.projectileSpread, 0) * direction;
                projectile.position = PlayerPosition;
                projectile.damageMultiplier = stats.damageMultiplier;
                projectile.source = SkillKind.Bolt;
                projectile.target = null;
                projectile.explosionRadius = 0;
                projectile.createdAt = Stats.Elapsed;
                projectile.life = stats.projectileLifetime;
                projectile.speed = stats.projectileSpeed;
                projectile.debuffs = stats.debuffs;
                projectile.body.sharedMaterial = MaterialFor(stats.color);
                projectile.root.position = PlayerPosition + Vector3.up * 0.9f;
                projectile.root.rotation = Quaternion.LookRotation(projectile.direction);
                projectile.root.gameObject.SetActive(true);
                _projectiles.Add(projectile);
            }
            if (!followUp)
            {
                _boltVolleysRemaining = stats.volleyCount - 1;
                _boltFollowUpAt = _boltVolleysRemaining > 0 ? Stats.Elapsed + stats.volleyInterval : -1;
                _boltFollowUpBurst = burst;
                _boltFollowUpDirection = direction;
            }
            if (burst) PlayTone(0);
        }

        private void TickProjectiles(float dt)
        {
            for (var i = _projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = _projectiles[i];
                if (projectile.source == SkillKind.Missile && projectile.target != null && _enemies.Contains(projectile.target))
                {
                    var targetDirection = projectile.target.root.position - projectile.position;
                    if (targetDirection.sqrMagnitude > 0.001f) projectile.direction = targetDirection.normalized;
                }
                var before = projectile.position;
                projectile.position += projectile.direction * projectile.speed * dt;
                projectile.life -= dt;
                var hit = false;
                for (var j = _enemies.Count - 1; j >= 0; j--)
                {
                    var segment = projectile.position - before;
                    var along = Mathf.Clamp01(Vector3.Dot(_enemies[j].root.position - before, segment) / Mathf.Max(0.0001f, segment.sqrMagnitude));
                    if ((_enemies[j].root.position - (before + along * segment)).sqrMagnitude > Mathf.Pow(_enemies[j].radius + 0.3f, 2)) continue;
                    if (projectile.source == SkillKind.Missile)
                    {
                        projectile.position = before + along * segment;
                        ExplodeMissile(projectile);
                    }
                    else
                    {
                        ApplyDebuffs(_enemies[j], projectile.debuffs, projectile.source);
                        DamageEnemy(j, projectile.damageMultiplier * Stats.BaseDamage);
                    }
                    hit = true;
                    break;
                }
                projectile.root.position = projectile.position + Vector3.up * 0.9f;
                if (projectile.source == SkillKind.Missile && !hit && projectile.life <= 0) ExplodeMissile(projectile);
                if (!hit && projectile.life > 0) continue;
                projectile.root.gameObject.SetActive(false);
                _projectilePool.Push(projectile);
                _projectiles.RemoveAt(i);
            }
        }

        private void TickOrbiters()
        {
            var stats = Parameters(SkillKind.Orbit);
            var count = stats.orbiterCount;
            for (var i = 0; i < _orbiters.Length; i++)
            {
                _orbiters[i].gameObject.SetActive(AutoAttack && SkillLevel(SkillKind.Orbit) > 0 && i < count);
                if (i >= count) continue;
                var angle = Stats.Elapsed * stats.orbitSpeed + i * Mathf.PI * 2 / count;
                _orbiters[i].position = PlayerPosition + new Vector3(Mathf.Cos(angle) * stats.radius, stats.orbitHeight, Mathf.Sin(angle) * stats.radius);
                _orbiters[i].Rotate(0, stats.bladeRotationSpeed * Time.deltaTime, 0);
            }
            if (!AutoAttack || SkillLevel(SkillKind.Orbit) == 0) return;
            for (var i = _enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemies[i];
                if (Stats.Elapsed < enemy.orbitAt) continue;
                for (var j = 0; j < count; j++)
                {
                    var difference = _orbiters[j].position - enemy.root.position;
                    difference.y = 0;
                    if (difference.sqrMagnitude > Mathf.Pow(enemy.radius + 0.6f, 2)) continue;
                    enemy.orbitAt = Stats.Elapsed + stats.hitInterval;
                    ApplyDebuffs(enemy, stats.debuffs, SkillKind.Orbit);
                    DamageEnemy(i, stats.damageMultiplier * Stats.BaseDamage);
                    break;
                }
            }
        }

        private void ApplyDebuffs(Enemy enemy, SurvivorDebuffConfig[] debuffs, SkillKind source, float expiresAt = float.PositiveInfinity)
        {
            foreach (var debuff in debuffs)
            {
                if (!debuff.enabled) continue;
                var until = Mathf.Min(expiresAt, Stats.Elapsed + debuff.duration);
                if (debuff.type == "Freeze" && until >= enemy.frozenUntil) { enemy.frozenUntil = until; enemy.freezeSource = source; }
                else if (debuff.type == "Slow")
                {
                    enemy.slowUntil = Mathf.Max(enemy.slowUntil, until);
                    enemy.slowMultiplier = debuff.moveSpeedMultiplier;
                    enemy.slowSource = source;
                }
                else if (debuff.type == "Bleed")
                {
                    if (Stats.Elapsed >= enemy.bleedUntil) enemy.nextBleedAt = Stats.Elapsed + debuff.tickInterval;
                    enemy.bleedUntil = Mathf.Max(enemy.bleedUntil, until);
                    enemy.bleedInterval = debuff.tickInterval;
                    enemy.bleedPercent = debuff.currentHealthPercent;
                    enemy.bleedSource = source;
                }
            }
        }

        private void TickBleeding()
        {
            for (var i = _enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemies[i];
                while (enemy.nextBleedAt > 0 && enemy.nextBleedAt <= Stats.Elapsed + 0.0001f && enemy.nextBleedAt <= enemy.bleedUntil + 0.0001f)
                {
                    enemy.nextBleedAt += enemy.bleedInterval;
                    DamageEnemy(i, enemy.health * enemy.bleedPercent);
                    if (enemy.health <= 0) break;
                }
                if (Stats.Elapsed >= enemy.bleedUntil) enemy.nextBleedAt = 0;
            }
        }

        private void DamageEnemy(int index, float damage)
        {
            var enemy = _enemies[index];
            if (Stats.Elapsed < enemy.guardUntil) damage *= 1 - enemy.damageReduction;
            enemy.health -= damage;
            enemy.flashUntil = Stats.Elapsed + 0.08f;
            _flash.SetColor("_Color", SurvivorPalette.Text);
            enemy.body.SetPropertyBlock(_flash);
            if (enemy.health <= 0) KillEnemy(index);
        }

        private void KillEnemy(int index)
        {
            var enemy = _enemies[index];
            CancelEnemyCast(enemy);
            var experience = _enemyCatalog.Get(enemy.id).experience;
            if (_gems.Count >= 512) _gems[_gems.Count - 1].value += experience;
            else
            {
                var gem = _gemPool.Count > 0 ? _gemPool.Pop() : CreateGem();
                gem.root.position = enemy.root.position + Vector3.up * 0.3f;
                gem.value = experience;
                gem.root.gameObject.SetActive(true);
                _gems.Add(gem);
            }
            enemy.root.gameObject.SetActive(false);
            _enemyPool[enemy.id].Push(enemy);
            _enemies.RemoveAt(index);
            Stats.Kills++;
        }

        private void TickGems(float dt)
        {
            for (var i = _gems.Count - 1; i >= 0; i--)
            {
                var gem = _gems[i];
                var target = PlayerPosition + Vector3.up * 0.4f;
                var distance = (gem.root.position - target).sqrMagnitude;
                if (distance < 25) gem.root.position = Vector3.MoveTowards(gem.root.position, target, (6 + Stats.Level * 0.3f) * dt);
                gem.root.Rotate(0, 90 * dt, 0);
                if (distance > 0.8f) continue;
                AddExperience(gem.value);
                gem.root.gameObject.SetActive(false);
                _gemPool.Push(gem);
                _gems.RemoveAt(i);
                PlayTone(4);
                if (IsChoosingReward) return;
            }
        }

        public void EndRun(bool won)
        {
            if (HasEnded) return;
            HasEnded = true;
            PendingModifications = 0;
            PendingSkills = 0;
            _pendingRewards.Clear();
            _rewardChoices.Clear();
            HasWon = won;
            IsPaused = false;
            _audio.UnPause();
            ApplyTimeScale();
            _bestKills = Mathf.Max(_bestKills, Stats.Kills);
            PlayerPrefs.SetInt("GameDevTools.Survivor.BestKills", _bestKills);
            PlayerPrefs.Save();
            PlayTone(won ? 2 : 1);
            Debug.Log("[Survivor] " + (won ? "Victory" : "Defeated") + " kills=" + Stats.Kills);
            RefreshHud(true);
        }

        public void ToggleTools()
        {
            if (Tools == null) return;
            if (Tools.IsOpen) Tools.Close();
            else { if (Tools.Console == null) RestartTools(); else Tools.Open(); }
        }

        public bool RestartTools(bool open = true)
        {
            if (Tools == null || !Tools.Restart()) return false;
            if (_registeredFont != null) Tools.RegisterFont(_registeredFont.name, _registeredFont);
            if (open) Tools.Open();
            return true;
        }

        private void ApplyTimeScale()
        {
            if (!_initialized) return;
            Time.timeScale = IsPaused || IsChoosingReward || HasEnded ? 0 : _gameSpeed;
            if (_audio == null) return;
            if (IsPaused || IsChoosingReward) _audio.Pause(); else _audio.UnPause();
        }
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static float FiniteClamp(float value, float min, float max) => IsFinite(value) ? Mathf.Clamp(value, min, max) : throw new ArgumentOutOfRangeException(nameof(value), "Value must be finite.");

        private void OnDestroy()
        {
            if (!_initialized) return;
            _lifetime.Cancel();
            _lifetime.Dispose();
            ReleaseHud();
            ReleaseWorld();
            if (Instance == this) { Instance = null; Time.timeScale = _originalTimeScale; }
        }

        private struct FrostZone { public Vector3 position; public float radius, createdAt, expiresAt, nextDamageAt, damageMultiplier, tickInterval; public SurvivorDebuffConfig[] debuffs; public RingEffect outer, middle, inner; }
        private sealed class Enemy
        {
            public Transform root, healthBar, bar;
            public Renderer body;
            public string id;
            public PrimitiveType shape;
            public int spawnWave;
            public SkillKind freezeSource, slowSource, bleedSource;
            public Vector3 knockbackVelocity, chargeDirection, previousPosition;
            public float sizeSample, radius, health, maxHealth, contactAt, orbitAt, frozenUntil, flashUntil, bleedUntil, nextBleedAt, bleedInterval, bleedPercent, slowUntil, slowMultiplier, knockbackDecay;
            public float chargeUntil, chargeSpeed, chargeDamage, guardUntil, damageReduction, hasteUntil, hasteMultiplier;
            public bool chargeHit;
            public EnemyCast cast;
            public List<EnemySkillState> skills = new();
        }
        private sealed class Projectile { public Transform root; public Renderer body; public Vector3 position, direction; public SkillKind source; public Enemy target; public float explosionRadius, damageMultiplier, createdAt, life, speed; public SurvivorDebuffConfig[] debuffs; }
        private sealed class Gem { public Transform root; public int value; }
    }
}
