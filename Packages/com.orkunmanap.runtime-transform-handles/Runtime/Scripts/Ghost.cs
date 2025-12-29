using System;
using UnityEngine;

namespace TransformHandles
{
    /// <summary>
    /// Represents a pivot point for handle manipulation.
    /// The Ghost transform is the center point around which objects are transformed.
    /// It tracks the initial state at interaction start and calculates deltas during manipulation.
    /// </summary>
    public class Ghost : MonoBehaviour
    {
        private Transform GhostTransform => transform;
        private TransformHandleManager _handleManager;
        private PosRotScale _initialProperties;

        /// <summary>
        /// Initializes the ghost with a reference to the handle manager.
        /// </summary>
        public virtual void Initialize()
        {
            _handleManager = TransformHandleManager.Instance;
        }

        /// <summary>
        /// Terminates and destroys this ghost object.
        /// </summary>
        public virtual void Terminate()
        {
            DestroyImmediate(gameObject);
        }

        /// <summary>
        /// Updates the ghost's transform to match the given position, rotation, and scale.
        /// </summary>
        /// <param name="average">The average position, rotation, and scale to apply.</param>
        public void UpdateGhostTransform(PosRotScale average)
        {
            GhostTransform.position = average.Position;
            GhostTransform.rotation = average.Rotation;
            GhostTransform.localScale = average.Scale;
        }

        /// <summary>
        /// Resets the ghost's transform to default values (zero position, identity rotation, unit scale).
        /// </summary>
        public void ResetGhostTransform()
        {
            GhostTransform.position = Vector3.zero;
            GhostTransform.rotation = Quaternion.identity;
            GhostTransform.localScale = Vector3.one;
        }

        /// <summary>
        /// Called when interaction starts. Stores the initial transform state for delta calculations.
        /// </summary>
        public virtual void OnInteractionStart()
        {
            _initialProperties = new PosRotScale()
            {
                Position = GhostTransform.position,
                Rotation = GhostTransform.rotation,
                Scale = GhostTransform.lossyScale
            };
        }

        /// <summary>
        /// Called during interaction. Calculates deltas and updates the transform group.
        /// </summary>
        /// <param name="handleType">The type of handle being manipulated.</param>
        public virtual void OnInteraction(HandleType handleType)
        {
            switch (handleType)
            {
                case HandleType.Position:
                    UpdatePosition();
                    break;
                case HandleType.Rotation:
                    UpdateRotation();
                    
                    break;
                case HandleType.Scale:
                    UpdateScale();
                    
                    break;
                case HandleType.PositionRotation:
                    UpdatePosition();
                    UpdateRotation();
                    
                    break;
                case HandleType.PositionScale:
                    UpdatePosition();
                    UpdateScale();

                    break;
                case HandleType.RotationScale:
                    UpdateRotation();
                    UpdateScale();
                    
                    break;
                case HandleType.All:
                    UpdatePosition();
                    UpdateRotation();
                    UpdateScale();
                    
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            ResetInitialGhostTransformProperties();
        }

        private void UpdatePosition()
        {
            var positionChange = GhostTransform.position - _initialProperties.Position;
            _handleManager.UpdateGroupPosition(this, positionChange);
        }

        private void UpdateRotation()
        {
            var rotationChange = GhostTransform.rotation * Quaternion.Inverse(_initialProperties.Rotation);
            _handleManager.UpdateGroupRotation(this, rotationChange);
        }

        private void UpdateScale()
        {
            var scaleChange= GhostTransform.localScale - _initialProperties.Scale;
            _handleManager.UpdateGroupScaleUpdate(this, scaleChange);
        }
        
        private void ResetInitialGhostTransformProperties()
        {
            _initialProperties.Position = GhostTransform.position;
            _initialProperties.Rotation = GhostTransform.rotation;
            _initialProperties.Scale = GhostTransform.lossyScale;
        }
    }
}