using UnityEngine;

public static class PreserveScaleOnScreenExtension
{
    public static void PreserveScaleOnScreen(this Transform transform, float currentFOV, float desiredSizeInPixels, Camera camera)
    {
       var position = transform.position;

        // Calculate the current distance to the camera, use near plane.
        var planes = GeometryUtility.CalculateFrustumPlanes(camera);
        var currentDistanceToCamera = Mathf.Abs(planes[4].GetDistanceToPoint(position));

        // Calculate the new scale based on the desired pixel size and the current distance and FOV
        var desiredSizeInWorldUnits = 2f * Mathf.Tan(currentFOV * 0.5f * Mathf.Deg2Rad) * desiredSizeInPixels / Screen.height;
        var newScale = desiredSizeInWorldUnits * currentDistanceToCamera;

        // Set the new scale of the game object
        transform.localScale = new Vector3(newScale, newScale, newScale);
    }
}