using Exiled.API.Interfaces;

namespace TestFPS
{
    public class Config : IConfig
    {
        public bool IsEnabled { get; set; } = true;
        public bool Debug { get; set; } = false;
        public int PingCooldown { get; set; } = 1;
        public int PingDuration { get; set; } = 5;
        public int PingLimit { get; set; } = 5;
    }
}