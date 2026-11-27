using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Sanmon.SurvivorDemo
{
    public sealed class SurvivorConfiguration
    {
        public const string DIRECTORY = "SurvivorDemo";
        public const string ASSET_DIRECTORY = "Assets/StreamingAssets/" + DIRECTORY;

        public SurvivorSkillCatalog skills;
        public SurvivorWaveCatalog waves;
        public SurvivorEnemyCatalog enemies;
        public SurvivorLevelCatalog levels;
        public SurvivorEnemySkillCatalog enemySkills;

        public static bool UsesWebRequest => Application.streamingAssetsPath.Contains("://");

        public static string FilePath(string fileName) => Path.Combine(Application.streamingAssetsPath, DIRECTORY, fileName);

        public static string ReadJson(string fileName) => File.ReadAllText(FilePath(fileName), Encoding.UTF8);

        public static SurvivorConfiguration Load()
        {
            return Parse(ReadJson("Skills.json"), ReadJson("Waves.json"), ReadJson("Enemies.json"), ReadJson("Level.json"), ReadJson("EnemySkills.json"));
        }

        /// <summary> Android 和 WebGL 的 StreamingAssets 通过异步请求读取, 桌面直接读取文件. </summary>
        public static IEnumerator LoadAsync(Action<SurvivorConfiguration> onLoaded)
        {
            if (!UsesWebRequest)
            {
                onLoaded(Load());
                yield break;
            }

            var names = new[] { "Skills.json", "Waves.json", "Enemies.json", "Level.json", "EnemySkills.json" };
            var json = new string[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                var url = FilePath(names[i]).Replace('\\', '/');
                using var request = UnityWebRequest.Get(url);
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                    throw new IOException("无法读取配置 " + names[i] + ": " + request.error);
                json[i] = request.downloadHandler.text;
            }
            onLoaded(Parse(json[0], json[1], json[2], json[3], json[4]));
        }

        public static SurvivorConfiguration Parse(string skills, string waves, string enemies, string levels, string enemySkills)
        {
            var configuration = new SurvivorConfiguration
            {
                skills = SurvivorSkills.Parse(skills),
                waves = SurvivorWaves.Parse(waves),
                enemies = SurvivorEnemies.Parse(enemies),
                levels = SurvivorLevels.Parse(levels),
                enemySkills = SurvivorEnemySkills.Parse(enemySkills)
            };
            configuration.waves.ValidateEnemies(configuration.enemies);
            configuration.enemies.ValidateSkills(configuration.enemySkills);
            return configuration;
        }

        internal static void ValidateNumber(float value, float min, float max, string label)
        {
            if (!SurvivorArena.IsFinite(value) || value < min || value > max)
                throw new ArgumentException(label + " 必须在 " + min + "–" + max + " 之间。");
        }

        public static void ValidateEnemyId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("怪物 ID 不能为空。");
            foreach (var character in id)
                if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
                    throw new ArgumentException("怪物 ID 只能使用字母、数字、下划线或短横线。");
        }
    }
}
