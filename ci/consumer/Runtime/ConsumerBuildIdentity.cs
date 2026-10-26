using System;

namespace ConsumerJourney
{
    [Serializable]
    public sealed class ConsumerBuildIdentity
    {
        public string runId, source, packageSource, lifecycleStage, buildId;
    }
}
