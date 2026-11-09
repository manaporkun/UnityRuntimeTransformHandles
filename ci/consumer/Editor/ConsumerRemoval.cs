using System;
using System.Threading;
using UnityEditor.PackageManager;

namespace ConsumerJourney
{
    public static class ConsumerRemoval
    {
        public static void RemoveDemos()
        {
            ConsumerBootstrap.Require(ConsumerBootstrap.Config.stage == "remove-demos", "Wrong Demos removal stage");
            var request = Client.Remove("com.orkunmanap.runtime-transform-handles.demos");
            var deadline = DateTime.UtcNow.AddSeconds(120);
            while (!request.IsCompleted)
            {
                ConsumerBootstrap.Require(DateTime.UtcNow < deadline, "UPM Demos removal timed out");
                Thread.Sleep(50);
            }
            ConsumerBootstrap.Require(request.Status == StatusCode.Success, "UPM Demos removal failed: " + request.Error?.message);
            ConsumerBootstrap.Observe();
        }
    }
}
