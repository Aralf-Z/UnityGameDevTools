using UnityEngine;

namespace GameConsole.UI
{
    /// <summary> 工具资源只从项目副本加载, 外部字体和宿主资源不经过此入口. </summary>
    internal static class ToolkitResources
    {
        internal const string Root = "Sanmon/GameDevTools/";

        internal static T Load<T>(string path) where T : Object => Resources.Load<T>(Root + path);

        internal static ResourceRequest LoadAsync<T>(string path) where T : Object => Resources.LoadAsync<T>(Root + path);
    }
}
