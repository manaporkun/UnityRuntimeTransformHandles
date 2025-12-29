using System.Collections.Generic;
using TransformHandles.Utils;
using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Manages a group of transforms that are controlled by a single handle.
    /// Handles position, rotation, and scale updates for all transforms in the group.
    /// </summary>
    public class TransformGroup
    {
        /// <summary>Gets the ghost (pivot point) associated with this group.</summary>
        public Ghost GroupGhost { get; }

        /// <summary>Gets the handle that controls this group.</summary>
        public Handle GroupHandle { get; }

        /// <summary>
        /// When true, transformations are centered on the combined bounds center.
        /// When false, transformations use the object pivots.
        /// </summary>
        public bool IsOriginOnCenter;

        /// <summary>Gets the set of transforms in this group.</summary>
        public HashSet<Transform> Transforms { get; }

        /// <summary>Gets the mapping of transforms to their mesh renderers (if any).</summary>
        public Dictionary<Transform, MeshRenderer> RenderersMap { get; }

        /// <summary>Gets the mapping of transforms to their cached bounds.</summary>
        public Dictionary<Transform, Bounds> BoundsMap { get; }

        /// <summary>
        /// Creates a new transform group with the specified ghost and handle.
        /// </summary>
        /// <param name="groupGhost">The ghost (pivot point) for this group.</param>
        /// <param name="groupHandle">The handle that controls this group.</param>
        public TransformGroup(Ghost groupGhost, Handle groupHandle)
        {
            GroupGhost = groupGhost;
            GroupHandle = groupHandle;

            Transforms = new HashSet<Transform>();
            RenderersMap = new Dictionary<Transform, MeshRenderer>();
            BoundsMap = new Dictionary<Transform, Bounds>();
        }

        /// <summary>
        /// Adds a transform to the group if it's not already relative to selected transforms.
        /// </summary>
        /// <param name="target">The transform to add.</param>
        /// <returns>True if the transform was added successfully, false otherwise.</returns>
        public bool AddTransform(Transform target)
        {
            if (IsTargetRelativeToSelectedOnes(target)) return false;

            var meshRenderer = target.GetComponent<MeshRenderer>();

            Transforms.Add(target);
            RenderersMap.Add(target, meshRenderer);
            BoundsMap.Add(target, meshRenderer != null ? meshRenderer.bounds : target.GetBounds());

            return true;
        }

        /// <summary>
        /// Removes a transform from the group.
        /// </summary>
        /// <param name="target">The transform to remove.</param>
        /// <returns>True if the group is now empty, false otherwise.</returns>
        public bool RemoveTransform(Transform target)
        {
            Transforms.Remove(target);
            RenderersMap.Remove(target);
            BoundsMap.Remove(target);

            return Transforms.Count == 0;
        }

        /// <summary>
        /// Updates the bounds for all transforms in the group.
        /// </summary>
        public void UpdateBounds()
        {
            foreach (var (target, meshRenderer) in RenderersMap)
            {
                var bounds = meshRenderer ? meshRenderer.bounds : target.GetBounds();
                BoundsMap[target] = bounds;
            }
        }

        /// <summary>
        /// Updates the position of all transforms in the group.
        /// </summary>
        /// <param name="positionChange">The position change to apply.</param>
        public void UpdatePositions(Vector3 positionChange)
        {
            foreach (var target in RenderersMap.Keys)
            {
                target.position += positionChange;
            }
        }

        /// <summary>
        /// Updates the rotation of all transforms in the group.
        /// </summary>
        /// <param name="rotationChange">The rotation change to apply.</param>
        public void UpdateRotations(Quaternion rotationChange)
        {
            var ghostPosition = GroupGhost.transform.position;
            var rotationAxis = rotationChange.normalized.eulerAngles;
            var rotationChangeMagnitude = rotationChange.eulerAngles.magnitude;

            foreach (var target in RenderersMap.Keys)
            {
                if (GroupHandle.space == Space.Self)
                {
                    target.position = rotationChange * (target.position - ghostPosition) + ghostPosition;
                    target.rotation = rotationChange * target.rotation;
                }
                else
                {
                    target.RotateAround(ghostPosition, rotationAxis, rotationChangeMagnitude);
                }
            }
        }

        /// <summary>
        /// Updates the scale of all transforms in the group.
        /// </summary>
        /// <param name="scaleChange">The scale change to apply.</param>
        public void UpdateScales(Vector3 scaleChange)
        {
            foreach (var (target, meshRenderer) in RenderersMap)
            {
                if (IsOriginOnCenter && meshRenderer != null)
                {
                    var oldCenter = meshRenderer.bounds.center;
                    target.localScale += scaleChange;

                    // ReSharper disable once Unity.InefficientPropertyAccess
                    var newCenter = meshRenderer.bounds.center;
                    var change = newCenter - oldCenter;
                    target.position -= change;
                }
                else
                {
                    target.localScale += scaleChange;
                }
            }
        }

        /// <summary>
        /// Gets the center point of a transform based on the origin setting.
        /// </summary>
        private Vector3 GetCenterPoint(Transform target)
        {
            return IsOriginOnCenter ? BoundsMap[target].center : target.position;
        }

        /// <summary>
        /// Calculates the average position, rotation, and scale of all transforms in the group.
        /// </summary>
        /// <returns>The average position, rotation, and scale.</returns>
        public PosRotScale GetAveragePosRotScale()
        {
            var space = GroupHandle.space;
            var averagePosRotScale = new PosRotScale();

            var centerPositions = new List<Vector3>();
            var sumQuaternion = Quaternion.identity;
            var transformsCount = Transforms.Count;

            foreach (var target in Transforms)
            {
                var centerPoint = GetCenterPoint(target);
                centerPositions.Add(centerPoint);

                if (space == Space.World) continue;
                sumQuaternion *= target.rotation;
            }

            var averagePosition = Vector3.zero;
            foreach (var centerPosition in centerPositions)
            {
                averagePosition += centerPosition;
            }
            averagePosition /= transformsCount;

            averagePosRotScale.Position = averagePosition;
            averagePosRotScale.Rotation = sumQuaternion;
            averagePosRotScale.Scale = Vector3.one;

            return averagePosRotScale;
        }

        /// <summary>
        /// Checks if the new target is a parent or child of any existing transform in the group.
        /// </summary>
        private bool IsTargetRelativeToSelectedOnes(Transform newTarget)
        {
            foreach (var transformInHash in Transforms)
            {
                if (transformInHash.IsDeepParentOf(newTarget)) return true;

                if (!newTarget.IsDeepParentOf(transformInHash)) continue;
                RemoveTransform(transformInHash);
                return false;
            }

            return false;
        }
    }
}
