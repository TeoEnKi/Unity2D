using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

public class FlockBehaviour : MonoBehaviour
{
    List<Obstacle> mObstacles = new List<Obstacle>();

    [SerializeField]
    GameObject[] Obstacles;

    [SerializeField]
    BoxCollider2D Bounds;
    float bounds_maxX;
    float bounds_minX;
    float bounds_maxY;
    float bounds_minY;


    public float TickDuration = 1.0f;
    public float TickDurationSeparationEnemy = 0.1f;
    public float TickDurationRandom = 1.0f;

    public int BoidIncr = 100;
    public bool useFlocking = false;
    public int BatchSize = 100;

    public List<Flock> flocks = new List<Flock>();

    [SerializeField] GameObject preinitBoids;
    List<Autonomous> unusedBoids = new List<Autonomous>();

    void Reset()
    {
        flocks = new List<Flock>()
    {
      new Flock()
    };
    }

    void Start()
    {
        bounds_maxX = Bounds.bounds.max.x;
        bounds_minX = Bounds.bounds.min.x;
        bounds_maxY = Bounds.bounds.max.y;
        bounds_minY = Bounds.bounds.min.y;

        foreach (Transform preinitBoid in preinitBoids.transform)
        {
            if (preinitBoid.transform.GetComponent<Autonomous>() != null)
            {
                unusedBoids.Add(preinitBoid.transform.GetComponent<Autonomous>());
            }
        }
        // Randomize obstacles placement.
        for (int i = 0; i < Obstacles.Length; ++i)
        {
            float x = UnityEngine.Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
            float y = UnityEngine.Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);
            Obstacles[i].transform.position = new Vector3(x, y, 0.0f);
            Obstacle obs = Obstacles[i].AddComponent<Obstacle>();
            Autonomous autono = Obstacles[i].AddComponent<Autonomous>();
            autono.MaxSpeed = 1.0f;
            obs.mCollider = Obstacles[i].GetComponent<CircleCollider2D>();
            mObstacles.Add(obs);
        }

        foreach (Flock flock in flocks)
        {
            CreateFlock(flock);
        }

        StartCoroutine(Coroutine_Flocking());

        StartCoroutine(Coroutine_Random());
        StartCoroutine(Coroutine_AvoidObstacles());
        StartCoroutine(Coroutine_SeparationWithEnemies());
        StartCoroutine(Coroutine_Random_Motion_Obstacles());
    }

    void CreateFlock(Flock flock)
    {
        for (int i = 0; i < flock.numBoids; ++i)
        {
            float x = UnityEngine.Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
            float y = UnityEngine.Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);

            AddBoid(x, y, flock);
        }
    }

    void Update()
    {
        //(boids_SpatialLookup, boids_StartIds) = UpdateSpatialLookup(flocks[0].mAutonomous, boids_SpatialLookup, boids_StartIds, flocks[0].separationDistance);
        //(enemies_SpatialLookup, enemies_StartIds) = UpdateSpatialLookup(flocks[1].mAutonomous, enemies_SpatialLookup, enemies_StartIds, flocks[0].separationDistance);
        HandleInputs();
        Rule_CrossBorder();
        Rule_CrossBorder_Obstacles();
    }

    void HandleInputs()
    {
        if (EventSystem.current.IsPointerOverGameObject() ||
           enabled == false)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            //if no more pre-init boids, instantiate them
            if (unusedBoids.Count == 0)
            {
                AddBoids(BoidIncr);
            }
            else
            {
                EnablePreinitBoids(BoidIncr, flocks[0]);
            }
        }
    }

    private void EnablePreinitBoids(int count, Flock flock)
    {
        for (int i = 0; i < count; ++i)
        {
            if (unusedBoids.Count == 0)
            {
                AddBoids(count - i);
                return;

            }
            float x = UnityEngine.Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
            float y = UnityEngine.Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);

            unusedBoids[0].gameObject.SetActive(true);
            unusedBoids[0].name = "Boid_" + flock.name + "_" + flock.mAutonomous.Count;
            unusedBoids[0].transform.position = new Vector3(x, y, 0.0f);
            Autonomous boid = unusedBoids[0].GetComponent<Autonomous>();
            flock.mAutonomous.Add(boid);
            boid.MaxSpeed = flock.maxSpeed;
            boid.RotationSpeed = flock.maxRotationSpeed;

            //remove the preinit boid from the group of unused preinit boids
            unusedBoids[0].transform.parent = null;
            unusedBoids.RemoveAt(0);
        }
        flocks[0].numBoids += count;
    }

    void AddBoids(int count)
    {
        for (int i = 0; i < count; ++i)
        {
            float x = UnityEngine.Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
            float y = UnityEngine.Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);

            AddBoid(x, y, flocks[0]);
        }
        flocks[0].numBoids += count;
    }

    void AddBoid(float x, float y, Flock flock)
    {
        GameObject obj = Instantiate(flock.PrefabBoid);
        obj.name = "Boid_" + flock.name + "_" + flock.mAutonomous.Count;
        obj.transform.position = new Vector3(x, y, 0.0f);
        Autonomous boid = obj.GetComponent<Autonomous>();
        flock.mAutonomous.Add(boid);
        boid.MaxSpeed = flock.maxSpeed;
        boid.RotationSpeed = flock.maxRotationSpeed;
    }

    static float Distance(Autonomous a1, Autonomous a2)
    {
        return (a1.transform.position - a2.transform.position).magnitude;
    }

    void Execute(Flock flock, int i)
    {
        Vector3 flockDir = Vector3.zero;
        Vector3 separationDir = Vector3.zero;
        Vector3 cohesionDir = Vector3.zero;

        float speed = 0.0f;
        float separationSpeed = 0.0f;

        int count = 0;
        int separationCount = 0;
        Vector3 steerPos = Vector3.zero;

        Autonomous curr = flock.mAutonomous[i];
        Parallel.For(0, flock.numBoids, j =>
        {
            Autonomous other = flock.mAutonomous[j];
            float dist = (curr.predictedPos - other.predictedPos).magnitude;
            if (i != j && dist < flock.visibility)
            {
                speed += other.Speed;
                flockDir += other.TargetDirection;
                steerPos += other.predictedPos;
                count++;
            }
            if (i != j)
            {
                if (dist < flock.separationDistance)
                {
                    Vector3 targetDirection = (
                      curr.predictedPos -
                      other.predictedPos).normalized;

                    separationDir += targetDirection;
                    separationSpeed += dist * flock.weightSeparation;
                }
            }
        });
        //for (int j = 0; j < flock.numBoids; ++j)
        //{
        //    Autonomous other = flock.mAutonomous[j];
        //    float dist = (curr.transform.position - other.transform.position).magnitude;
        //    if (i != j && dist < flock.visibility)
        //    {
        //        speed += other.Speed;
        //        flockDir += other.TargetDirection;
        //        steerPos += other.transform.position;
        //        count++;
        //    }
        //    if (i != j)
        //    {
        //        if (dist < flock.separationDistance)
        //        {
        //            Vector3 targetDirection = (
        //              curr.transform.position -
        //              other.transform.position).normalized;

        //            separationDir += targetDirection;
        //            separationSpeed += dist * flock.weightSeparation;
        //        }
        //    }
        //}
        if (count > 0)
        {
            speed = speed / count;
            flockDir = flockDir / count;
            flockDir.Normalize();

            steerPos = steerPos / count;
        }

        if (separationCount > 0)
        {
            separationSpeed = separationSpeed / count;
            separationDir = separationDir / separationSpeed;
            separationDir.Normalize();
        }

        curr.TargetDirection =
          flockDir * speed * (flock.useAlignmentRule ? flock.weightAlignment : 0.0f) +
          separationDir * separationSpeed * (flock.useSeparationRule ? flock.weightSeparation : 0.0f) +
          (steerPos - curr.predictedPos) * (flock.useCohesionRule ? flock.weightCohesion : 0.0f);
    }


    IEnumerator Coroutine_Flocking()
    {
        //runs everytick duration and not just once in start
        while (true)
        {
            if (useFlocking)
            {
                foreach (Flock flock in flocks)
                {
                    List<Autonomous> autonomousList = flock.mAutonomous;
                    Parallel.For(0, autonomousList.Count, i =>
                    {
                        Execute(flock, i);
                    });
                    //for (int i = 0; i < autonomousList.Count; ++i)
                    //{
                    //    Execute(flock, i);
                    //    //if (i % BatchSize == 0)
                    //    //{
                    //    //    //run in next frame after execute is runned a number of times
                    //    //    yield return null;
                    //    //}
                    //}
                    //////run in next frame
                    ////yield return null;
                }
            }
            //run next while loop after tickduration
            yield return new WaitForSeconds(TickDuration);
        }
    }


    void SeparationWithEnemies_Internal(
      List<Autonomous> boids,
      List<Autonomous> enemies,
      float sepDist,
      float sepWeight)
    {
        for (int i = 0; i < boids.Count; ++i)
        {
            for (int j = 0; j < enemies.Count; ++j)
            {
                float dist = (
                  enemies[j].predictedPos -
                  boids[i].predictedPos).magnitude;
                if (dist < sepDist)
                {
                    Vector3 targetDirection = (
                      boids[i].predictedPos -
                      enemies[j].predictedPos).normalized;

                    boids[i].TargetDirection += targetDirection;
                    boids[i].TargetDirection.Normalize();

                    boids[i].TargetSpeed += dist * sepWeight;
                    boids[i].TargetSpeed /= 2.0f;
                }
            }
        }
    }

    IEnumerator Coroutine_SeparationWithEnemies()
    {
        while (true)
        {
            foreach (Flock flock in flocks)
            {
                if (!flock.useFleeOnSightEnemyRule || flock.isPredator) continue;

                foreach (Flock enemies in flocks)
                {
                    if (!enemies.isPredator) continue;

                    SeparationWithEnemies_Internal(
                      flock.mAutonomous,
                      enemies.mAutonomous,
                      flock.enemySeparationDistance,
                      flock.weightFleeOnSightEnemy);
                }
                //yield return null;
            }
            yield return null;
        }
    }

    IEnumerator Coroutine_AvoidObstacles()
    {
        while (true)
        {
            foreach (Flock flock in flocks)
            {
                if (flock.useAvoidObstaclesRule)
                {
                    List<Autonomous> autonomousList = flock.mAutonomous;
                    for (int i = 0; i < autonomousList.Count; ++i)
                    {
                        for (int j = 0; j < mObstacles.Count; ++j)
                        {
                            float dist = (
                              mObstacles[j].transform.position -
                              autonomousList[i].predictedPos).magnitude;
                            if (dist < mObstacles[j].AvoidanceRadius)
                            {
                                Vector3 targetDirection = (
                                  autonomousList[i].predictedPos -
                                  mObstacles[j].transform.position).normalized;

                                autonomousList[i].TargetDirection += targetDirection * flock.weightAvoidObstacles;
                                autonomousList[i].TargetDirection.Normalize();
                            }
                        }
                    }
                }
                //yield return null;
            }
            yield return null;
        }
    }
    IEnumerator Coroutine_Random_Motion_Obstacles()
    {
        while (true)
        {
            for (int i = 0; i < Obstacles.Length; ++i)
            {
                Autonomous autono = Obstacles[i].GetComponent<Autonomous>();
                float rand = UnityEngine.Random.Range(0.0f, 1.0f);
                autono.TargetDirection.Normalize();
                float angle = Mathf.Atan2(autono.TargetDirection.y, autono.TargetDirection.x);

                if (rand > 0.5f)
                {
                    angle += Mathf.Deg2Rad * 45.0f;
                }
                else
                {
                    angle -= Mathf.Deg2Rad * 45.0f;
                }
                Vector3 dir = Vector3.zero;
                dir.x = Mathf.Cos(angle);
                dir.y = Mathf.Sin(angle);

                autono.TargetDirection += dir * 0.1f;
                autono.TargetDirection.Normalize();
                //Debug.Log(autonomousList[i].TargetDirection);

                float speed = UnityEngine.Random.Range(1.0f, autono.MaxSpeed);
                autono.TargetSpeed += speed;
                autono.TargetSpeed /= 2.0f;
            }
            yield return new WaitForSeconds(2.0f);
        }
    }
    IEnumerator Coroutine_Random()
    {
        while (true)
        {
            foreach (Flock flock in flocks)
            {
                if (flock.useRandomRule)
                {
                    List<Autonomous> autonomousList = flock.mAutonomous;
                    for (int i = 0; i < autonomousList.Count; ++i)
                    {
                        float rand = UnityEngine.Random.Range(0.0f, 1.0f);
                        autonomousList[i].TargetDirection.Normalize();
                        float angle = Mathf.Atan2(autonomousList[i].TargetDirection.y, autonomousList[i].TargetDirection.x);

                        if (rand > 0.5f)
                        {
                            angle += Mathf.Deg2Rad * 45.0f;
                        }
                        else
                        {
                            angle -= Mathf.Deg2Rad * 45.0f;
                        }
                        Vector3 dir = Vector3.zero;
                        dir.x = Mathf.Cos(angle);
                        dir.y = Mathf.Sin(angle);

                        autonomousList[i].TargetDirection += dir * flock.weightRandom;
                        autonomousList[i].TargetDirection.Normalize();
                        //Debug.Log(autonomousList[i].TargetDirection);

                        float speed = UnityEngine.Random.Range(1.0f, autonomousList[i].MaxSpeed);
                        autonomousList[i].TargetSpeed += speed * flock.weightSeparation;
                        autonomousList[i].TargetSpeed /= 2.0f;
                    }
                }
                //yield return null;
            }
            yield return new WaitForSeconds(TickDurationRandom);
        }
    }
    void Rule_CrossBorder_Obstacles()
    {
        for (int i = 0; i < Obstacles.Length; ++i)
        {
            Autonomous autono = Obstacles[i].GetComponent<Autonomous>();
            Vector3 pos = autono.predictedPos;
            if (autono.predictedPos.x > Bounds.bounds.max.x)
            {
                pos.x = Bounds.bounds.min.x;
            }
            if (autono.predictedPos.x < Bounds.bounds.min.x)
            {
                pos.x = Bounds.bounds.max.x;
            }
            if (autono.predictedPos.y > Bounds.bounds.max.y)
            {
                pos.y = Bounds.bounds.min.y;
            }
            if (autono.predictedPos.y < Bounds.bounds.min.y)
            {
                pos.y = Bounds.bounds.max.y;
            }
            autono.predictedPos = pos;
        }

        //for (int i = 0; i < Obstacles.Length; ++i)
        //{
        //  Autonomous autono = Obstacles[i].GetComponent<Autonomous>();
        //  Vector3 pos = autono.transform.position;
        //  if (autono.transform.position.x + 5.0f > Bounds.bounds.max.x)
        //  {
        //    autono.TargetDirection.x = -1.0f;
        //  }
        //  if (autono.transform.position.x - 5.0f < Bounds.bounds.min.x)
        //  {
        //    autono.TargetDirection.x = 1.0f;
        //  }
        //  if (autono.transform.position.y + 5.0f > Bounds.bounds.max.y)
        //  {
        //    autono.TargetDirection.y = -1.0f;
        //  }
        //  if (autono.transform.position.y - 5.0f < Bounds.bounds.min.y)
        //  {
        //    autono.TargetDirection.y = 1.0f;
        //  }
        //  autono.TargetDirection.Normalize();
        //}
    }

    void Rule_CrossBorder()
    {
        foreach (Flock flock in flocks)
        {
            List<Autonomous> autonomousList = flock.mAutonomous;
            if (flock.bounceWall)
            {
                Parallel.For(0, autonomousList.Count, i =>
                {
                    Vector3 pos = autonomousList[i].predictedPos;
                    if (autonomousList[i].predictedPos.x + 5.0f > bounds_maxX)
                    {
                        autonomousList[i].TargetDirection.x = -1.0f;
                    }
                    if (autonomousList[i].predictedPos.x - 5.0f < bounds_minX)
                    {
                        autonomousList[i].TargetDirection.x = 1.0f;
                    }
                    if (autonomousList[i].predictedPos.y + 5.0f > bounds_maxY)
                    {
                        autonomousList[i].TargetDirection.y = -1.0f;
                    }
                    if (autonomousList[i].predictedPos.y - 5.0f < bounds_minY)
                    {
                        autonomousList[i].TargetDirection.y = 1.0f;
                    }
                    autonomousList[i].TargetDirection.Normalize();
                });
                //for (int i = 0; i < autonomousList.Count; ++i)
                //{
                //    Vector3 pos = autonomousList[i].transform.position;
                //    if (autonomousList[i].transform.position.x + 5.0f > Bounds.bounds.max.x)
                //    {
                //        autonomousList[i].TargetDirection.x = -1.0f;
                //    }
                //    if (autonomousList[i].transform.position.x - 5.0f < Bounds.bounds.min.x)
                //    {
                //        autonomousList[i].TargetDirection.x = 1.0f;
                //    }
                //    if (autonomousList[i].transform.position.y + 5.0f > Bounds.bounds.max.y)
                //    {
                //        autonomousList[i].TargetDirection.y = -1.0f;
                //    }
                //    if (autonomousList[i].transform.position.y - 5.0f < Bounds.bounds.min.y)
                //    {
                //        autonomousList[i].TargetDirection.y = 1.0f;
                //    }
                //    autonomousList[i].TargetDirection.Normalize();
                //}
            }
            else
            {
                Parallel.For(0, autonomousList.Count, i => 
                { 
                    Vector3 pos = autonomousList[i].predictedPos;
                    if (autonomousList[i].predictedPos.x > bounds_maxX)
                    {
                        pos.x = bounds_minX;
                    }
                    if (autonomousList[i].predictedPos.x < bounds_minX)
                    {
                        pos.x = bounds_maxX;
                    }
                    if (autonomousList[i].predictedPos.y > bounds_maxY)
                    {
                        pos.y = bounds_minY;
                    }
                    if (autonomousList[i].predictedPos.y < bounds_minY)
                    {
                        pos.y = bounds_maxY;
                    }
                    autonomousList[i].predictedPos = pos;
                });


                //for (int i = 0; i < autonomousList.Count; ++i)
                //{
                //    Vector3 pos = autonomousList[i].transform.position;
                //    if (autonomousList[i].transform.position.x > Bounds.bounds.max.x)
                //    {
                //        pos.x = Bounds.bounds.min.x;
                //    }
                //    if (autonomousList[i].transform.position.x < Bounds.bounds.min.x)
                //    {
                //        pos.x = Bounds.bounds.max.x;
                //    }
                //    if (autonomousList[i].transform.position.y > Bounds.bounds.max.y)
                //    {
                //        pos.y = Bounds.bounds.min.y;
                //    }
                //    if (autonomousList[i].transform.position.y < Bounds.bounds.min.y)
                //    {
                //        pos.y = Bounds.bounds.max.y;
                //    }
                //    autonomousList[i].transform.position = pos;
                //}
            }
        }
    }
    //containing the list of auto id and its cellkey
    List<Entry> boids_SpatialLookup = new List<Entry>();
    //using the cell key, get the value of element (cell id) to know where that list of auto that are in the same cell start
    List<int> boids_StartIds = new List<int>();
    //combine the list from both flocks to use as an argueent

    List<Entry> enemies_SpatialLookup = new List<Entry>();
    //using the cell key, get the value of element (cell id) to know where that list of auto that are in the same cell start
    List<int> enemies_StartIds = new List<int>();

    private (List<Entry> spatialLookup, List<int> startIds) UpdateSpatialLookup(List<Autonomous> autoList, List<Entry> spatialLookup, List<int> startIds, float radius)
    {
        //multithreading the tasks 
        Parallel.For(0, autoList.Count, i =>
        {
            //converting the world position to a cell id
            (int cellX, int cellY) = PositionToCellCoord(autoList[i].predictedPos, radius);
            //hash the cell id to get a hash cell key
            //cell Key must be non-negative
            uint cellKey = GetHashCellKey(spatialLookup, HashCell(cellX, cellY));
            spatialLookup[i] = new Entry(i, cellKey);
            //reset the values in the list of start indices
            startIds[i] = int.MaxValue;
        });

        //sort by hashCellkey
        spatialLookup.Sort();

        //find the start indices and of each hashcellkey in the spatial lookup
        Parallel.For(0, autoList.Count, i =>
        {
            uint key = spatialLookup[i].hashCellKey;
            uint keyPrev = i == 0 ? uint.MaxValue : spatialLookup[i - 1].hashCellKey;
            if (key != keyPrev)
            {
                startIds[(int)key] = i;
            }
        });

        return (spatialLookup, startIds);
    }

    private (int cellX, int cellY) PositionToCellCoord(Vector3 pos, float radius)
    {
        int cellX = (int)(pos.x / radius);
        int cellY = (int)(pos.y / radius);

        return (cellX, cellY);
    }

    private uint HashCell(int cellX, int cellY)
    {
        uint a = (uint)cellX * 15823;
        uint b = (uint)cellY * 9737333;

        return a + b;
    }

    private uint GetHashCellKey(List<Entry> spatialLookup, uint hashCell)
    {
        return hashCell % (uint)spatialLookup.Count;
    }

}

internal class Entry : IComparable<Entry>
{
    public int i;
    public uint hashCellKey;

    public Entry(int i, uint hashCellKey)
    {
        this.i = i;
        this.hashCellKey = hashCellKey;
    }
    public int CompareTo(Entry other)
    {
        if (other == null)
        {
            return 1;
        }
        return hashCellKey.CompareTo(other.hashCellKey);
    }
}