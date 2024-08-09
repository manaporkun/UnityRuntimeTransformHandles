using System;
using UnityEngine;

namespace TransformHandles
{
    public class Ghost : MonoBehaviour
    {
        private Transform ghostTransform => transform;
        private TransformHandleManager _handleManager;
        private PosRotScale _initialProperties;

        public virtual void Initialize()
        {
            _handleManager = TransformHandleManager.instance;
        }

        public virtual void Terminate()
        {
            DestroyImmediate(gameObject);
        }

        public void UpdateGhostTransform(PosRotScale average)
        {
            average.ApplyToTransform(ghostTransform);
        }

        public void ResetGhostTransform()
        {
            ghostTransform.position = Vector3.zero;
            ghostTransform.rotation = Quaternion.identity;
            ghostTransform.localScale = Vector3.one;
        }

        public virtual void OnInteractionStart()
        {
            _initialProperties = PosRotScale.FromTransform(ghostTransform);
        }

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
            var positionChange = ghostTransform.position - _initialProperties.Position;
            _handleManager.UpdateGroupPosition(this, positionChange);
        }

        private void UpdateRotation()
        {
            var rotationChange = ghostTransform.rotation * Quaternion.Inverse(_initialProperties.Rotation);
            _handleManager.UpdateGroupRotation(this, rotationChange);
        }

        private void UpdateScale()
        {
            var scaleChange= ghostTransform.localScale - _initialProperties.Scale;
            _handleManager.UpdateGroupScaleUpdate(this, scaleChange);
        }
        
        private void ResetInitialGhostTransformProperties()
        {
            _initialProperties.Position = ghostTransform.position;
            _initialProperties.Rotation = ghostTransform.rotation;
            _initialProperties.Scale = ghostTransform.lossyScale;
        }
    }
}