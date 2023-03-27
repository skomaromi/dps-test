using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DapperTest
{
    [UpdateAfter(typeof(VehicleManagementSystem))]
    public partial class VehicleLerpSystem : SystemBase
    {
        private BeginSimulationEntityCommandBufferSystem beginSimulationSystem;

        protected override void OnCreate()
        {
            beginSimulationSystem = World.GetExistingSystem<BeginSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            double time = Time.ElapsedTime;
            
            Entities
                .ForEach((ref Translation vehicleTranslation, ref Rotation vehicleRotation, in Vehicle vehicle, in VehicleMovement vehicleMovement) =>
            {
                double timeElapsed = time - vehicleMovement.timeLastMoved; 
                float alpha = (float)(timeElapsed / vehicle.movementIntervalSeconds);

                float3 currentPosition = vehicleMovement.currentPosition;
                float3 nextPosition = vehicleMovement.nextPosition;
                float3 interpolatedPosition = math.lerp(currentPosition, nextPosition, alpha);
                
                vehicleTranslation.Value = interpolatedPosition;

                float3 direction = nextPosition - currentPosition;
                vehicleRotation.Value = quaternion.LookRotationSafe(direction, new float3(0f, 1f, 0f));
            }).ScheduleParallel();
        }
    }
}
