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
        internal HashSet<Transform> Transforms { get; }

        /// <summary>
        /// The transforms in this group as a read-only, live view (the actual manipulated objects,
        /// not the pivot). Do not cache across add/remove.
        /// </summary>
        public IReadOnlyCollection<Transform> Targets => Transforms;

        /// <summary>Gets the mapping of transforms to their mesh renderers (if any).</summary>
        internal Dictionary<Transform, MeshRenderer> RenderersMap { get; }

        /// <summary>Gets the mapping of transforms to their cached bounds.</summary>
        internal Dictionary<Transform, Bounds> BoundsMap { get; }

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

        private readonly Dictionary<Transform, PosRotScale> _scaleStart = new Dictionary<Transform, PosRotScale>();
        private Vector3 _scaleCenter, _ghostStartScale;

        internal void BeginInteraction()
        {
            EndInteraction();
            if (!GroupHandle.UniformGroupScale || GroupHandle.Type != HandleType.Scale ||
                GroupHandle.Axes != HandleAxes.XYZ || Transforms.Count < 2 || GroupGhost == null) return;
            _scaleCenter = GroupGhost.transform.position;
            _ghostStartScale = GroupGhost.transform.localScale;
            foreach (var target in Transforms)
                if (target != null) _scaleStart.Add(target, new PosRotScale
                { Position = target.position, Rotation = target.rotation, Scale = target.localScale });
        }

        internal void EndInteraction() => _scaleStart.Clear();

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private void UpdateUniformScale()
        {
            if (_scaleStart.Count != Transforms.Count || _scaleStart.Count < 2 || GroupGhost == null) return;
            var current = GroupGhost.transform.localScale;
            if (!Finite(current) || !Finite(_ghostStartScale) ||
                _ghostStartScale.x <= 0 || _ghostStartScale.y <= 0 || _ghostStartScale.z <= 0) return;
            var factor = current.x / _ghostStartScale.x;
            if (!(factor > 0) || float.IsInfinity(factor) ||
                !Mathf.Approximately(factor, current.y / _ghostStartScale.y) ||
                !Mathf.Approximately(factor, current.z / _ghostStartScale.z)) return;
            // Validate the entire result before mutating any member.
            foreach (var pair in _scaleStart)
            {
                if (pair.Key == null || !Transforms.Contains(pair.Key) ||
                    !Finite(pair.Value.Scale * factor) ||
                    !Finite(_scaleCenter + (pair.Value.Position - _scaleCenter) * factor)) return;
            }
            foreach (var pair in _scaleStart)
            {
                pair.Key.localScale = pair.Value.Scale * factor;
                pair.Key.position = _scaleCenter + (pair.Value.Position - _scaleCenter) * factor;
            }
        }

        internal bool HasDestroyedTargets()
        {
            foreach (var target in Transforms)
            {
                if (target == null) return true;
            }

            return false;
        }

        internal bool HasLiveTargets()
        {
            foreach (var target in Transforms)
            {
                if (target != null) return true;
            }

            return false;
        }

        /// <summary>
        /// Removes independently destroyed targets from all group maps.
        /// The caller owns the optional buffer; when null a local list is used.
        /// </summary>
        internal int PruneDestroyedTargets(List<Transform> removed = null)
        {
            var targetsToRemove = removed ?? new List<Transform>();
            targetsToRemove.Clear();

            foreach (var target in Transforms)
            {
                if (target == null) targetsToRemove.Add(target);
            }

            foreach (var target in targetsToRemove) RemoveTransform(target);
            return targetsToRemove.Count;
        }

        /// <summary>
        /// Adds a transform to the group if it's not null and not already relative to selected transforms.
        /// </summary>
        /// <param name="target">The transform to add.</param>
        /// <returns>True if the transform was added successfully, false if target is null or relative to existing transforms.</returns>
        public bool AddTransform(Transform target)
        {
            return AddTransform(target, null);
        }

        /// <summary>
        /// Internal overload used by the manager so descendant replacements can be removed from the
        /// manager's ownership index. The supplied buffer is cleared by this method.
        /// </summary>
        internal bool AddTransform(Transform target, List<Transform> replaced)
        {
            replaced?.Clear();

            if (ReferenceEquals(target, null))
            {
                Debug.LogWarning("Cannot add null transform to group.");
                return false;
            }

            if (target == null)
            {
                Debug.LogWarning("Cannot add a destroyed transform to group.");
                return false;
            }

            // A public TransformGroup may outlive an independently destroyed member; clear those
            // before any hierarchy checks so IsDeepParentOf never dereferences a destroyed receiver.
            PruneDestroyedTargets();

            if (IsTargetRelativeToSelectedOnes(target, replaced)) return false;

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
        internal void UpdateBounds()
        {
            foreach (var (target, meshRenderer) in RenderersMap)
            {
                if (target == null) continue;

                var bounds = meshRenderer ? meshRenderer.bounds : target.GetBounds();
                BoundsMap[target] = bounds;
            }
        }

        /// <summary>
        /// Updates the position of all transforms in the group.
        /// </summary>
        /// <param name="positionChange">The position change to apply.</param>
        internal void UpdatePositions(Vector3 positionChange)
        {
            foreach (var target in RenderersMap.Keys)
            {
                if (target == null) continue;
                target.position += positionChange;
            }
        }

        /// <summary>
        /// Updates the rotation of all transforms in the group.
        /// </summary>
        /// <param name="rotationChange">The rotation change to apply.</param>
        internal void UpdateRotations(Quaternion rotationChange)
        {
            var ghostPosition = GroupGhost.transform.position;

            if (GroupHandle.Space == Space.Self)
            {
                foreach (var target in RenderersMap.Keys)
                {
                    if (target == null) continue;
                    target.position = rotationChange * (target.position - ghostPosition) + ghostPosition;
                    target.rotation = rotationChange * target.rotation;
                }
            }
            else
            {
                // Decompose the delta quaternion into a true axis + angle. The previous code used
                // rotationChange.eulerAngles as the axis and its magnitude as the angle, which is
                // not a valid axis/angle decomposition (it only happened to work for small
                // single-axis rotations and diverged for compound/off-axis or large deltas).
                rotationChange.ToAngleAxis(out var angleInDegrees, out var rotationAxis);

                // ToAngleAxis returns angle 0 (axis (1,0,0)) for the identity delta; skip the
                // no-op work and guard against any non-finite axis from float drift.
                if (Mathf.Approximately(angleInDegrees, 0f) || float.IsNaN(rotationAxis.x)) return;

                foreach (var target in RenderersMap.Keys)
                {
                    if (target == null) continue;
                    target.RotateAround(ghostPosition, rotationAxis, angleInDegrees);
                }
            }
        }

        /// <summary>
        /// Updates the scale of all transforms in the group.
        /// </summary>
        /// <param name="scaleChange">The scale change to apply.</param>
        internal void UpdateScales(Vector3 scaleChange)
        {
            if (GroupHandle.UniformGroupScale && GroupHandle.Type == HandleType.Scale && Transforms.Count > 1)
            {
                if (GroupHandle.Axes == HandleAxes.XYZ) UpdateUniformScale();
                return;
            }
            foreach (var (target, meshRenderer) in RenderersMap)
            {
                if (target == null) continue;

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
            var space = GroupHandle.Space;
            var averagePosRotScale = new PosRotScale();
            var liveCount = 0;
            var sumQuaternion = Quaternion.identity;
            var averagePosition = Vector3.zero;

            foreach (var target in Transforms)
            {
                if (target == null) continue;

                liveCount++;
                averagePosition += GetCenterPoint(target);

                if (space == Space.World) continue;
                sumQuaternion *= target.rotation;
            }

            if (liveCount == 0)
            {
                averagePosRotScale.Position = Vector3.zero;
                averagePosRotScale.Rotation = Quaternion.identity;
                averagePosRotScale.Scale = Vector3.one;
                return averagePosRotScale;
            }

            averagePosition /= liveCount;

            averagePosRotScale.Position = averagePosition;
            var reference = GroupHandle.LocalSpaceReference;
            averagePosRotScale.Rotation = space == Space.Self && reference != null && Transforms.Contains(reference)
                ? reference.rotation : sumQuaternion;
            averagePosRotScale.Scale = Vector3.one;

            return averagePosRotScale;
        }

        /// <summary>
        /// Checks if the new target is a parent or child of any existing transform in the group.
        /// A new parent replaces all contained descendants.
        /// </summary>
        private bool IsTargetRelativeToSelectedOnes(Transform newTarget, List<Transform> replaced)
        {
            var descendants = replaced ?? new List<Transform>();

            foreach (var transformInHash in Transforms)
            {
                if (transformInHash == null) continue;

                if (transformInHash.IsDeepParentOf(newTarget)) return true;

                if (newTarget.IsDeepParentOf(transformInHash)) descendants.Add(transformInHash);
            }

            foreach (var descendant in descendants) RemoveTransform(descendant);
            return false;
        }
    }
}
