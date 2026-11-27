using System;
using System.Globalization;
using RedSaw.CommandLineInterface;
using UnityEngine;

namespace Sanmon.SurvivorDemo
{
    public enum EnemyKind { Grunt, Runner, Tank }
    public enum SkillKind { Bolt = 0, Orbit = 1, Nova = 2, Frost = 3, Lightning = 4, Flame = 5, Missile = 6, Venom = 7, Dash = 8 }
    public enum SurvivorRewardKind { Skill = 0, Modification = 1 }

    public readonly struct ArenaPoint
    {
        public readonly float x;
        public readonly float z;
        public ArenaPoint(float x, float z) { this.x = x; this.z = z; }
        public Vector3 Position => new(x, 0, z);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.0}, {1:0.0})", x, z);
    }

    [DebugObject("Survivor / Player")]
    public sealed class SurvivorStats
    {
        [DebugInfo("生命", Color = "#65DDD3")] public float Health = 100;
        [DebugInfo("生命上限")] public float MaxHealth = 100;
        /// <summary> 每秒恢复的生命值, 0 表示不恢复. </summary>
        [DebugInfo("每秒生命恢复", Color = "#65DDD3")] public float HealthRegenerationPerSecond = 1;
        [DebugInfo("移动速度")] public float MoveSpeed = 6;
        [DebugInfo("基础伤害")] public float BaseDamage = 10;
        [DebugInfo("无敌")] public bool GodMode;
        [DebugInfo("等级", Color = "#E9CC7B")] public int Level = 1;
        [DebugInfo("累计经验")] public int Experience;
        [DebugInfo("击杀")] public int Kills;
        [DebugInfo("波次")] public int Wave = 1;
        [DebugInfo("生存秒数")] public float Elapsed;
        /// <summary> 到达下一等级所需的累计总经验. </summary>
        public int ExperienceRequired { get; internal set; } = 8;
    }

    // 冷色石质试验场，青色胶囊玩家与珊瑚色敌人；金色经验与技能强调色。
    // 暖色斜向主光搭配冷色环境光；所有材质、HUD 和生成音效共用此调色板。
    public static class SurvivorPalette
    {
        public static readonly Color Floor = Hex("172E39");
        public static readonly Color Grid = Hex("294551");
        public static readonly Color Stone = Hex("3D5660");
        public static readonly Color Player = Hex("65DDD3");
        public static readonly Color Enemy = Hex("F28673");
        public static readonly Color Tank = Hex("CC668A");
        public static readonly Color Gold = Hex("E9CC7B");
        public static readonly Color Frost = Hex("FFFFFF");
        public static readonly Color Shockwave = Hex("C7A05A");
        public static readonly Color Violet = Hex("B4A5EB");
        public static readonly Color Text = Hex("F5F0E5");
        public static readonly Color Muted = Hex("94ABB4");
        public static readonly Color Shadow = Hex("0C1B25");
        public static readonly Color Light = Hex("FFE8C5");
        public static Color Alpha(Color color, float alpha) { color.a = alpha; return color; }
        private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
    }
}
