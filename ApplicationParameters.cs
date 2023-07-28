using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AvatarTemp
{
    public class ApplicationParameters
    {
        public const string SectionName = "ApplicationParameters";

        public bool DryRun { get; set; }
        public float LesFetchDelaySec { get; set; }
        public int LesFetchDelayRandomDeltaMs { get; set; }
        public string AvatarFilenameTemplate { get; set; } = null!;
    }
}
