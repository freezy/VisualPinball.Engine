using VisualPinball.Unity.DebugAndPhysicsComunicationProxy;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity.Game
{
    /// <summary>
    /// Proxy used by core Visual Pinball Engine to comunicate with Debug and Physics Engine
    /// </summary>
    public static class DPProxy // DebugAndPhysicsComunicationProxy
    {
        private static IDebugUI _debugUI = null;
        public static IDebugUI debugUI
        {
            get => _debugUI;
            set { _debugUI = value; } // this is used to register DebugUI
        }

        private static IPhysicsEngine _physicsEngine = new VPX_PhysicsEngineProxyClient();
        public static IPhysicsEngine physicsEngine
        {
            get => _physicsEngine;
            set { _physicsEngine = value; }
        }

        // ====================================================================

        public static void OnRegisterFlipper(Entity entity, string name)
        {
            _physicsEngine?.OnRegisterFlipper(entity, name);
            _debugUI?.OnRegisterFlipper(entity, name);
        }

        public static void OnPhysicsUpdate(int numSteps, float processingTime)
        {
            _physicsEngine?.OnPhysicsUpdate(numSteps, processingTime);
            _debugUI?.OnPhysicsUpdate(numSteps, processingTime);
        }

        public static void OnCreateBall(Entity entity, float3 position, float3 velocity, float radius, float mass)
        {
            _physicsEngine?.OnCreateBall(entity, position, velocity, mass, radius);
            _debugUI?.OnCreateBall(entity);
        }

        public static void OnRotateToEnd(Entity entity)
        {
            _physicsEngine?.OnRotateToEnd(entity);
        }

        public static void OnRotateToStart(Entity entity)
        {
            _physicsEngine?.OnRotateToStart(entity);
        }
    }
}
