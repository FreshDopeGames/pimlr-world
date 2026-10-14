using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace JUTPS.AI
{
    public class JUPathFinder
    {
        /// <summary>
        /// Calculates the path from the source position to a target position. It is necessary to bake NavMesh in the scene.
        /// </summary>
        /// <param name="SourcePosition">Source position</param>
        /// <param name="TargetPosition">Target position</param>
        /// <returns></returns>
        public static Vector3[] CalculatePath(Vector3 SourcePosition, Vector3 TargetPosition, int NavmeshArea = 1)
        {
            //Check Navmesh Existence
            NavMeshHit hitNv;
            NavMesh.SamplePosition(SourcePosition, out hitNv, 100, NavmeshArea);
            if (!hitNv.hit)
            {
                Debug.LogWarning("Unable to calculate path, make sure the Navmesh in your scene is baked");
                return new Vector3[0] { };
            }

            // > [ Old Code ]
            //Calculate path
            //NavMeshPath navmesh_path = new NavMeshPath();

            //if (NavMesh.FindClosestEdge(TargetPosition, out NavMeshHit hit, NavmeshArea))
            //{
            //    NavMesh.CalculatePath(SourcePosition, hit.position, NavmeshArea, navmesh_path);
            //}
            //Get path
            //Vector3[] Path = navmesh_path.corners;

            //Return the path
            //return Path;
            // < [ Old Code ]

            //Calculate path
            NavMeshPath navmesh_path = new NavMeshPath();
            //NavMesh.CalculatePath(SourcePosition, hitNv.position, NavmeshArea, navmesh_path);

            // PIMLR: aim at the closest WALKABLE POINT to the target, not the closest navmesh EDGE. FindClosestEdge
            // returns the nearest boundary (a curb, a car's carve hole, a wall), which is often metres beside or
            // behind the player - so every chaser converged on the same 'arbitrary' spot instead of on the player.
            // The edge lookup is kept only as a fallback when the target is far off the navmesh.
            if (NavMesh.SamplePosition(TargetPosition, out NavMeshHit hitt, 3f, NavmeshArea) ||
                NavMesh.FindClosestEdge(TargetPosition, out hitt, NavmeshArea))
            {
                NavMesh.CalculatePath(SourcePosition, hitt.position, NavmeshArea, navmesh_path);
                //return navmesh_path.corners;
            }


            //If still invalid
            if (navmesh_path.status == NavMeshPathStatus.PathPartial || navmesh_path.status == NavMeshPathStatus.PathInvalid)
            {
                //Get valid source position
                NavMeshHit hit;
                NavMesh.SamplePosition(SourcePosition, out hit, 10, NavmeshArea);
                //Get valid END position
                NavMeshHit hitEnd;
                NavMesh.SamplePosition(TargetPosition, out hitEnd, 10, NavmeshArea);

                if (!hit.hit || !hitEnd.hit) { Debug.LogWarning("Could not calculate NavMesh path, invalid target/source position"); return new Vector3[0] { }; }

                //Set position
                NavMesh.CalculatePath(hit.position, hitEnd.position, NavmeshArea, navmesh_path);
            }

            // >[ Old Code ]
            /* 
            //If cannot do pathfinding with current position, create a path with the closest navmesh edge
            if (navmesh_path.status == NavMeshPathStatus.PathInvalid)
            {
                NavMeshHit hit;
                NavMesh.SamplePosition(SourcePosition, out hit, 10, NavmeshArea);

                //Could not calculate path
                if (!hit.hit) { Debug.LogWarning("Could not calculate NavMesh path, invalid target/source position"); return new Vector3[0] { }; }

                NavMesh.CalculatePath(hit.position, TargetPosition, NavmeshArea, navmesh_path);
            }*/

            // < [ Old Code ]


            //Get path
            Vector3[] Path = navmesh_path.corners;

            //Return the path
            return Path;

            

            // [ Old Code ]
        }
        /// <summary>
        /// Calculates the path from the source position to a target position. It is necessary to bake NavMesh in the scene.
        /// </summary>
        /// <param name="SourcePosition">Source position</param>
        /// <param name="TargetPosition">Target position</param>
        /// <returns></returns>
        public static Vector3[] CalculatePath(Transform SourcePosition, Transform TargetPosition)
        {
            //Calculate path
            NavMeshPath navmesh_path = new NavMeshPath();
            NavMesh.CalculatePath(SourcePosition.position, TargetPosition.position, NavMesh.AllAreas, navmesh_path);

            //If cannot do pathfinding with current position, create a path with the closest navmesh edge
            if(navmesh_path.status == NavMeshPathStatus.PathInvalid)
            {
                NavMeshHit hit;
                NavMesh.FindClosestEdge(SourcePosition.position, out hit, NavMesh.AllAreas);
                NavMesh.CalculatePath(hit.position, TargetPosition.position, NavMesh.AllAreas, navmesh_path);
            }

            //Get path
            Vector3[] Path = navmesh_path.corners;

            //Return the path
            return Path;
        }

        /// <summary>
        /// Draw the path by lines
        /// </summary>
        /// <param name="path">path to draw</param>
        public static void VisualizePath(Vector3[] path)
        {
            Color color = Color.white;
            color.a = 0.2f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Debug.DrawLine(path[i], path[i] + Vector3.up * 0.1f, Color.red);
                Debug.DrawLine(path[i], path[i + 1], color);
            }
        }


        public static Vector3 GetClosestWalkablePoint(Vector3 targetPosition, float offsetDirection = 0.2f)
        {
            Vector3 position = Vector3.zero;

            // PIMLR: the result of SamplePosition was never checked. When the target stood more than 2 m from the
            // navmesh (sidewalk, steps, a car roof) the 'closest point' was garbage, and chasers walked to it.
            // Widen the search step by step, and fall back to the raw target position if nothing is found.
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(targetPosition, out hit, 2, NavMesh.AllAreas) &&
                !NavMesh.SamplePosition(targetPosition, out hit, 6, NavMesh.AllAreas))
            {
                return targetPosition;
            }

            Vector3 dir = (targetPosition - hit.position).normalized;
            position = hit.position - dir * offsetDirection;

            return position;
        }
    }
}