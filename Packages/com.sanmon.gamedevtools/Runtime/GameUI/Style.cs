using System;
using UnityEngine;

namespace GameConsole.UI
{
    /// <summary> 项目资源的名称和相对路径映射, 路径相对于 Sanmon/GameDevTools. </summary>
    public sealed class Style : ScriptableObject
    {
        [InspectorName("UI 样式")]
        public ToolkitUIStyleEntry[] uiStyles = Array.Empty<ToolkitUIStyleEntry>();

        [InspectorName("字体")]
        public ToolkitFontEntry[] fonts = Array.Empty<ToolkitFontEntry>();

        /// <summary> 工具独立语言包, 首个有效项作为默认和缺词回退, 路径相对于工具资源根目录. </summary>
        [InspectorName("语言")]
        public ToolkitLanguageEntry[] languages = Array.Empty<ToolkitLanguageEntry>();
    }

    /// <summary> 一个 UI 样式的显示名称和工具资源根目录内的相对目录. </summary>
    [Serializable]
    public sealed class ToolkitUIStyleEntry
    {
        [InspectorName("名称")]
        public string name = string.Empty;

        [InspectorName("路径")]
        public string path = string.Empty;
    }

    /// <summary> 共用字体的显示名称和工具资源根目录内的加载路径. </summary>
    [Serializable]
    public sealed class ToolkitFontEntry
    {
        [InspectorName("名称")]
        public string name = string.Empty;

        [InspectorName("路径")]
        public string path = string.Empty;
    }

    /// <summary> 语言代码, 原生名称和语言包目录. </summary>
    [Serializable]
    public sealed class ToolkitLanguageEntry
    {
        [InspectorName("代码")]
        public string code = string.Empty;

        [InspectorName("名称")]
        public string name = string.Empty;

        [InspectorName("路径")]
        public string path = string.Empty;
    }
}
