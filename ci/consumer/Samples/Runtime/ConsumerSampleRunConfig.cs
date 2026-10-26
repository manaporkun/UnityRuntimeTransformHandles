using UnityEngine;

namespace ConsumerJourney
{
    public sealed class ConsumerSampleRunConfig : ScriptableObject
    {
        public string runId, source, packageSource;
        public string[] scenes;
    }
}
