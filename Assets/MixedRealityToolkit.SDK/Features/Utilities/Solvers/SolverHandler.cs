// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Input;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Utilities.Solvers
{
    /// <summary>
    /// This class handles the solver components that are attached to this <see href="https://docs.unity3d.com/ScriptReference/GameObject.html">GameObject</see>
    /// </summary>
    public class SolverHandler : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Tracked object to calculate position and orientation from. If you want to manually override and use a scene object, use the TransformTarget field.")]
        private TrackedObjectType trackedTargetType = TrackedObjectType.Head;

        /// <summary>
        /// Tracked object to calculate position and orientation from. If you want to manually override and use a scene object, use the TransformTarget field.
        /// </summary>
        public TrackedObjectType TrackedTargetType
        {
            get { return trackedTargetType; }
            set
            {
                if (trackedTargetType != value)
                {
                    trackedTargetType = value;
                    RefreshTrackedObject();
                }
            }
        }

        [SerializeField]
        [Tooltip("")]
        private Handedness trackedHandness = Handedness.Both;

        /// <summary>
        /// TODO: Put comment here
        /// </summary>
        public Handedness TrackedHandness
        {
            get { return trackedHandness; }
            set
            {
                if (trackedHandness != value)
                {
                    trackedHandness = value;
                    RefreshTrackedObject();
                }
            }
        }

        [SerializeField]
        [Tooltip("When using the hand to calculate position and orientation, use this specific joint.")]
        private TrackedHandJoint trackedHandJoint = TrackedHandJoint.Palm;

        /// <summary>
        /// When using the hand to calculate position and orientation, use this specific joint.
        /// </summary>
        public TrackedHandJoint TrackedHandJoint
        {
            get { return trackedHandJoint; }
            set
            {
                if (trackedHandJoint != value)
                {
                    trackedHandJoint = value;
                    RefreshTrackedObject();
                }
            }
        }

        [SerializeField]
        [Tooltip("Manual override for TrackedObjectToReference if you want to use a scene object. Leave empty if you want to use head, motion-tracked controllers, or motion-tracked hands.")]
        private Transform transformOverride;

        [SerializeField]
        [Tooltip("Add an additional offset of the tracked object to base the solver on. Useful for tracking something like a halo position above your head or off the side of a controller.")]
        private Vector3 additionalOffset;

        /// <summary>
        /// Add an additional offset of the tracked object to base the solver on. Useful for tracking something like a halo position above your head or off the side of a controller.
        /// </summary>
        public Vector3 AdditionalOffset
        {
            get { return additionalOffset; }
            set
            {
                if (additionalOffset != value)
                {
                    additionalOffset = value;
                    RefreshTrackedObject();
                }
            }
        }

        [SerializeField]
        [Tooltip("Add an additional rotation on top of the tracked object. Useful for tracking what is essentially the up or right/left vectors.")]
        private Vector3 additionalRotation;

        /// <summary>
        /// Add an additional rotation on top of the tracked object. Useful for tracking what is essentially the up or right/left vectors.
        /// </summary>
        public Vector3 AdditionalRotation
        {
            get { return additionalRotation; }
            set
            {
                if (additionalRotation != value)
                {
                    additionalRotation = value;
                    RefreshTrackedObject();
                }
            }
        }

        [SerializeField]
        [Tooltip("Whether or not this SolverHandler calls SolverUpdate() every frame. Only one SolverHandler should manage SolverUpdate(). This setting does not affect whether the Target Transform of this SolverHandler gets updated or not.")]
        private bool updateSolvers = true;

        /// <summary>
        /// Whether or not this SolverHandler calls SolverUpdate() every frame. Only one SolverHandler should manage SolverUpdate(). This setting does not affect whether the Target Transform of this SolverHandler gets updated or not.
        /// </summary>
        public bool UpdateSolvers
        {
            get { return updateSolvers; }
            set { updateSolvers = value; }
        }

        /// <summary>
        /// The position the solver is trying to move to.
        /// </summary>
        public Vector3 GoalPosition { get; set; }

        /// <summary>
        /// The rotation the solver is trying to rotate to.
        /// </summary>
        public Quaternion GoalRotation { get; set; }

        /// <summary>
        /// The scale the solver is trying to scale to.
        /// </summary>
        public Vector3 GoalScale { get; set; }

        /// <summary>
        /// Alternate scale.
        /// </summary>
        public Vector3Smoothed AltScale { get; set; }

        /// <summary>
        /// The timestamp the solvers will use to calculate with.
        /// </summary>
        public float DeltaTime { get; set; }

        /// <summary>
        /// The target transform that the solvers will act upon.
        /// </summary>
        public Transform TransformTarget
        {
            get
            {
                if (trackingTarget == null)
                {
                    RefreshTrackedObject();
                }

                return trackingTarget?.transform;
            }
        }

        // Hidden GameObject managed by this component attached as a child to the tracked target type (i.e head, hand etc)
        private GameObject trackingTarget;

        protected readonly List<Solver> solvers = new List<Solver>();

        private float lastUpdateTime;

        private IMixedRealityHandJointService HandJointService => handJointService ?? (handJointService = (InputSystem as IMixedRealityDataProviderAccess)?.GetDataProvider<IMixedRealityHandJointService>());
        private IMixedRealityHandJointService handJointService = null;

        private IMixedRealityInputSystem inputSystem = null;

        /// <summary>
        /// The active instance of the input system.
        /// </summary>
        protected IMixedRealityInputSystem InputSystem
        {
            get
            {
                if (inputSystem == null)
                {
                    MixedRealityServiceRegistry.TryGetService<IMixedRealityInputSystem>(out inputSystem);
                }
                return inputSystem;
            }
        }

        #region MonoBehaviour Implementation

        private void Awake()
        {
            GoalScale = Vector3.one;
            AltScale = new Vector3Smoothed(Vector3.one, 0.1f);
            DeltaTime = Time.deltaTime;
            lastUpdateTime = Time.realtimeSinceStartup;

            solvers.AddRange(GetComponents<Solver>());
        }

        private void Start()
        {
            RefreshTrackedObject();
        }

        private void Update()
        {
            DeltaTime = Time.realtimeSinceStartup - lastUpdateTime;
            lastUpdateTime = Time.realtimeSinceStartup;
        }

        private void LateUpdate()
        {
            if (UpdateSolvers)
            {
                //Before calling solvers, update goal to be the transform so that working and transform will match
                GoalPosition = transform.position;
                GoalRotation = transform.rotation;
                GoalScale = transform.localScale;

                for (int i = 0; i < solvers.Count; ++i)
                {
                    Solver solver = solvers[i];

                    if (solver != null && solver.enabled)
                    {
                        solver.SolverUpdateEntry();
                    }
                }
            }
        }

        protected void OnDestroy()
        {
            DetachFromCurrentTrackedObject();
        }

        #endregion MonoBehaviour Implementation

        /// <summary>
        /// Clears the transform target and attaches to the current <see cref="TrackedTargetType"/>.
        /// </summary>
        public void RefreshTrackedObject()
        {
            DetachFromCurrentTrackedObject();
            AttachToNewTrackedObject();
        }

        public void SetTransformOverride(Transform target)
        {
            if (target != null)
            {
                this.transformOverride = target;
            }
        }

        public void SetSolvers(Solver[] newSolvers)
        {
            if (newSolvers != null)
            {
                this.solvers.Clear();
                this.solvers.AddRange(newSolvers);
            }
        }

        protected virtual void DetachFromCurrentTrackedObject()
        {
            if (trackingTarget != null)
            {
                DestroyImmediate(trackingTarget);
                trackingTarget = null;
            }
        }

        protected virtual void AttachToNewTrackedObject()
        {
            Transform target = null;
            if (TrackedTargetType == TrackedObjectType.Head)
            {
                target = CameraCache.Main.transform;
            }
            else if (TrackedTargetType == TrackedObjectType.MotionController)
            {
                if (this.TrackedHandness == Handedness.Both)
                {
                    target = GetMotionController(Handedness.Left);
                    if (target == null)
                    {
                        target = GetMotionController(Handedness.Right);
                    }
                }
                else
                {
                    target = GetMotionController(this.TrackedHandness);
                }
            }
            else if (TrackedTargetType == TrackedObjectType.HandJoint)
            {
                if (this.TrackedHandness == Handedness.Both)
                {
                    target = HandJointService?.RequestJointTransform(this.TrackedHandJoint, Handedness.Left);
                    if (target == null)
                    {
                        target = HandJointService?.RequestJointTransform(this.TrackedHandJoint, Handedness.Right);
                    }
                }
                else
                {
                    target = HandJointService?.RequestJointTransform(this.TrackedHandJoint, this.TrackedHandness);
                }
            }
            else if (TrackedTargetType == TrackedObjectType.CustomOverride)
            {
                target = this.transformOverride;
            }

            TrackTransform(target);
        }

        private void TrackTransform(Transform target)
        {
            if (trackingTarget != null || target == null)
            {
                Debug.LogWarning("Could not track transform as current GameObject target is not null or new target is null");
                return;
            }

            string name = string.Format("SolverHandler Target on {0} with offset {1}, {2}", target.gameObject.name, AdditionalOffset, AdditionalRotation);
            trackingTarget = new GameObject(name);
            trackingTarget.hideFlags = HideFlags.HideInHierarchy;

            trackingTarget.transform.parent = target;
            trackingTarget.transform.localPosition = Vector3.Scale(AdditionalOffset, trackingTarget.transform.localScale);
            trackingTarget.transform.localRotation = Quaternion.Euler(AdditionalRotation);
        }

        private Transform GetMotionController(Handedness handness)
        {
            // TODO: test InputSystem == null for for-loop
            foreach (IMixedRealityController controller in InputSystem?.DetectedControllers)
            {
                if (controller.ControllerHandedness == handness)
                {
                    if (controller.Visualizer == null ||
                        controller.Visualizer.GameObjectProxy == null || controller.Visualizer.GameObjectProxy.transform == null)
                    {
                        return null;
                    }

                    return controller.Visualizer.GameObjectProxy.transform;
                }
            }

            return null;
        }
    }
}