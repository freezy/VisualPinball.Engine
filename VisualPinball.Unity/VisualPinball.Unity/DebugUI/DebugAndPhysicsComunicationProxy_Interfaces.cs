using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity.DebugAndPhysicsComunicationProxy
{
    public enum Params
    {
        Physics_FlipperAcc = 1,
        Physics_FlipperOffScale = 2,
        Physics_FlipperOnNearEndScale = 3,
        Physics_FlipperNumOfDegreeNearEnd = 4,
        Physics_FlipperMass = 5,
    }

    /// <summary>
    /// Comunication interface to VisualPinball.Engine.Unity.ImgGUI	
    /// </summary>
    public interface IDebugUI
    {
        void OnRegisterFlipper(Entity entity, string name);
        void OnPhysicsUpdate(int numSteps, float processingTime);
        void OnCreateBall(Entity entity);
    }

    public struct FlipperState
    {
        public float angle;
        public bool solenoid;

        public FlipperState(float _angle, bool _solenoid)
        {
            angle = _angle;
            solenoid = _solenoid;
        }
    }

    /// <summary>
    /// Comunication interface to PhysicsEngine.
    /// For VPX-Physics see VPX_PhysicsEngine
    /// </summary>
    public interface IPhysicsEngine
    {
        void OnRegisterFlipper(Entity entity, string name);
        void OnPhysicsUpdate(int numSteps, float processingTime);
        void OnCreateBall(Entity entity, float3 position, float3 velocity, float radius, float mass);
        void OnRotateToEnd(Entity entity);
        void OnRotateToStart(Entity entity);
        bool UsePureEntity();
        void ManualBallRoller(Entity entity, float3 targetPosition);

        // ========================================================================== accesible from DebugUI ===
        bool GetFlipperState(Entity entity, out FlipperState flipperState);
        float GetFloat(Params param);
        void SetFloat(Params param, float val);
    }
}
