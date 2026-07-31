using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

[DisallowMultipleComponent]
public sealed class FlockManager : MonoBehaviour
{
    private const int ThreadGroupSize = 256;
    private const int BoidStride = sizeof(float) * 8;
    private const int UIntStride = sizeof(uint);
    private const int ForceZoneStride = sizeof(float) * 12;
    private const int ObstacleStride = sizeof(float) * 16;

    private static readonly int BoidsReadId = Shader.PropertyToID("_BoidsRead");
    private static readonly int BoidsWriteId = Shader.PropertyToID("_BoidsWrite");
    private static readonly int CellCountsId = Shader.PropertyToID("_CellCounts");
    private static readonly int CellBoidIndicesId = Shader.PropertyToID("_CellBoidIndices");
    private static readonly int OverflowCountId = Shader.PropertyToID("_OverflowCount");
    private static readonly int ForceZonesId = Shader.PropertyToID("_ForceZones");
    private static readonly int ObstaclesId = Shader.PropertyToID("_Obstacles");

    private static readonly int BoidCountId = Shader.PropertyToID("_BoidCount");
    private static readonly int GridCellCountId = Shader.PropertyToID("_GridCellCount");
    private static readonly int GridDimensionsId = Shader.PropertyToID("_GridDimensions");
    private static readonly int MaxBoidsPerCellId = Shader.PropertyToID("_MaxBoidsPerCell");
    private static readonly int PreyCellSearchRadiusId = Shader.PropertyToID("_PreyCellSearchRadius");
    private static readonly int PredatorCellSearchRadiusId = Shader.PropertyToID("_PredatorCellSearchRadius");
    private static readonly int BoundsCenterId = Shader.PropertyToID("_BoundsCenter");
    private static readonly int BoundsSizeId = Shader.PropertyToID("_BoundsSize");
    private static readonly int CellSizeId = Shader.PropertyToID("_CellSize");
    private static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
    private static readonly int NeighborRadiusId = Shader.PropertyToID("_NeighborRadius");
    private static readonly int SeparationRadiusId = Shader.PropertyToID("_SeparationRadius");
    private static readonly int CohesionWeightId = Shader.PropertyToID("_CohesionWeight");
    private static readonly int SeparationWeightId = Shader.PropertyToID("_SeparationWeight");
    private static readonly int AlignmentWeightId = Shader.PropertyToID("_AlignmentWeight");
    private static readonly int BoundaryWeightId = Shader.PropertyToID("_BoundaryWeight");
    private static readonly int MinSpeedId = Shader.PropertyToID("_MinSpeed");
    private static readonly int MaxSpeedId = Shader.PropertyToID("_MaxSpeed");
    private static readonly int MaxForceId = Shader.PropertyToID("_MaxForce");
    private static readonly int PredatorFractionId = Shader.PropertyToID("_PredatorFraction");
    private static readonly int PreyFleeRadiusId = Shader.PropertyToID("_PreyFleeRadius");
    private static readonly int PreyFleeWeightId = Shader.PropertyToID("_PreyFleeWeight");
    private static readonly int PredatorHuntRadiusId = Shader.PropertyToID("_PredatorHuntRadius");
    private static readonly int PredatorChaseWeightId = Shader.PropertyToID("_PredatorChaseWeight");
    private static readonly int PredatorSpeedMultiplierId = Shader.PropertyToID("_PredatorSpeedMultiplier");
    private static readonly int PredatorForceMultiplierId = Shader.PropertyToID("_PredatorForceMultiplier");
    private static readonly int PredatorFlockingMultiplierId = Shader.PropertyToID("_PredatorFlockingMultiplier");
    private static readonly int ForceZoneCountId = Shader.PropertyToID("_ForceZoneCount");
    private static readonly int ObstacleCountId = Shader.PropertyToID("_ObstacleCount");
    private static readonly int SimulationTimeId = Shader.PropertyToID("_SimulationTime");
    private static readonly int MaxEnvironmentAccelerationId = Shader.PropertyToID("_MaxEnvironmentAcceleration");
    private static readonly int RandomSeedId = Shader.PropertyToID("_RandomSeed");

    [Header("References")]
    [SerializeField] private ComputeShader boidsCompute;
    [SerializeField] private VisualEffect visualEffect;

    [Header("Population")]
    [SerializeField, Min(1)] private int boidCount = 32768;
    [SerializeField] private int randomSeed = 1;

    [Header("Voxel grid")]
    [SerializeField, Min(0.1f)] private float cellSize = 3f;
    [SerializeField, Min(1)] private int maxBoidsPerCell = 64;
    [SerializeField] private Vector3 simulationBounds = new(80f, 40f, 80f);

    [Header("Flocking")]
    [Tooltip("Distance dans laquelle un boid considère les membres de sa propre espèce.")]
    [SerializeField, Min(0.01f)] private float neighborRadius = 3f;
    [Tooltip("Distance minimale recherchée entre tous les boids, espèces confondues.")]
    [SerializeField, Min(0.01f)] private float separationRadius = 1.1f;
    [Tooltip("Attraction vers le centre des voisins de la même espèce.")]
    [SerializeField, Min(0f)] private float cohesionWeight = 0.8f;
    [Tooltip("Évitement des boids trop proches. Commencer entre 1 et 2.")]
    [SerializeField, Min(0f)] private float separationWeight = 1.7f;
    [Tooltip("Tendance à adopter la direction des voisins de la même espèce.")]
    [SerializeField, Min(0f)] private float alignmentWeight = 1.2f;
    [Tooltip("Force douce qui ramène les boids vers l'intérieur des bounds.")]
    [SerializeField, Min(0f)] private float boundaryWeight = 2f;

    [Header("Prey / predator")]
    [Tooltip("Proportion de prédateurs créée lors de l'initialisation. 0.01 correspond à environ 1 %.")]
    [SerializeField, Range(0f, 0.2f)] private float predatorFraction = 0.01f;
    [Tooltip("Distance à laquelle une proie détecte et fuit un prédateur.")]
    [SerializeField, Min(0.01f)] private float preyFleeRadius = 6f;
    [Tooltip("Priorité de la fuite par rapport aux trois règles de flocking.")]
    [SerializeField, Min(0f)] private float preyFleeWeight = 2.5f;
    [Tooltip("Distance à laquelle un prédateur peut choisir la proie la plus proche.")]
    [SerializeField, Min(0.01f)] private float predatorHuntRadius = 12f;
    [Tooltip("Priorité de la poursuite pour un prédateur ayant trouvé une proie.")]
    [SerializeField, Min(0f)] private float predatorChaseWeight = 1.5f;
    [Tooltip("Multiplicateur des vitesses minimale et maximale des prédateurs.")]
    [SerializeField, Min(0.1f)] private float predatorSpeedMultiplier = 1.25f;
    [Tooltip("Multiplicateur de Max Force pour permettre aux prédateurs de virer plus rapidement.")]
    [SerializeField, Min(0.1f)] private float predatorForceMultiplier = 1.5f;
    [Tooltip("Importance du flocking entre prédateurs. Une faible valeur les garde plus indépendants.")]
    [SerializeField, Min(0f)] private float predatorFlockingMultiplier = 0.25f;

    [Header("Movement")]
    [Tooltip("Vitesse minimale en unités par seconde. Évite que le flock se fige.")]
    [SerializeField, Min(0f)] private float minSpeed = 2f;
    [Tooltip("Vitesse maximale en unités par seconde.")]
    [SerializeField, Min(0.01f)] private float maxSpeed = 8f;
    [Tooltip("Accélération maximale en unités/s². Plus élevée = virages plus serrés et mouvement plus nerveux.")]
    [SerializeField, Min(0.01f)] private float maxForce = 5f;
    [Tooltip("Plafond du pas de simulation, pas une force. 0.0333 évite les bonds au prix d'un ralenti sous 30 FPS.")]
    [SerializeField, Range(0.001f, 0.1f)] private float maximumDeltaTime = 0.0333f;

    [Header("Environment and obstacles")]
    [Tooltip("Plafond séparé pour la somme des courants, vortex et turbulences.")]
    [SerializeField, Min(0.01f)] private float maxEnvironmentAcceleration = 10f;
    [Tooltip("Nombre maximal de BoidForceZone actives envoyées au GPU.")]
    [SerializeField, Min(1)] private int maxForceZones = 32;
    [Tooltip("Nombre maximal de BoidObstacle actifs envoyés au GPU.")]
    [SerializeField, Min(1)] private int maxObstacles = 64;

    [Header("Diagnostics")]
    [SerializeField] private bool monitorGridOverflow = true;
    [SerializeField, Min(0.1f)] private float overflowCheckInterval = 1f;

    public int BoidCount => boidCount;
    public uint LastOverflowCount { get; private set; }
    public Vector3Int GridDimensions => _gridDimensions;

    private GraphicsBuffer _boidsA;
    private GraphicsBuffer _boidsB;
    private GraphicsBuffer _cellCounts;
    private GraphicsBuffer _cellBoidIndices;
    private GraphicsBuffer _overflowCount;
    private GraphicsBuffer _forceZones;
    private GraphicsBuffer _obstacles;

    private BoidForceZone.GpuData[] _forceZoneUpload;
    private BoidObstacle.GpuData[] _obstacleUpload;
    private int _activeForceZoneCount;
    private int _activeObstacleCount;

    private int _initializeKernel;
    private int _clearGridKernel;
    private int _buildGridKernel;
    private int _simulateKernel;

    private Vector3Int _gridDimensions;
    private int _gridCellCount;
    private int _allocatedBoidCount;
    private int _allocatedMaxBoidsPerCell;
    private float _allocatedCellSize;
    private Vector3 _allocatedBounds;
    private int _allocatedRandomSeed;
    private float _allocatedPredatorFraction;
    private int _allocatedMaxForceZones;
    private int _allocatedMaxObstacles;

    private float _nextOverflowCheckTime;
    private bool _overflowReadbackPending;
    private bool _kernelsFound;
    private bool _warnedAboutMissingReferences;
    private bool _warnedAboutMissingVfxProperties;
    private bool _warnedAboutForceZoneCapacity;
    private bool _warnedAboutObstacleCapacity;

    private void OnEnable()
    {
        TryCreateResources();
    }

    private void Update()
    {
        if (!TryCreateResources())
        {
            return;
        }

        UpdateShaderParameters();
        UploadSceneInfluences();
        ClearAndBuildGrid();
        Simulate();
        PublishCurrentBuffer();
        RequestOverflowReadbackIfNeeded();
    }

    private void OnDisable()
    {
        ReleaseResources();
    }

    private void OnDestroy()
    {
        ReleaseResources();
    }

    private void OnValidate()
    {
        boidCount = Mathf.Max(1, boidCount);
        cellSize = Mathf.Max(0.1f, cellSize);
        maxBoidsPerCell = Mathf.Max(1, maxBoidsPerCell);
        simulationBounds.x = Mathf.Max(cellSize, simulationBounds.x);
        simulationBounds.y = Mathf.Max(cellSize, simulationBounds.y);
        simulationBounds.z = Mathf.Max(cellSize, simulationBounds.z);
        neighborRadius = Mathf.Max(0.01f, neighborRadius);
        separationRadius = Mathf.Clamp(separationRadius, 0.01f, neighborRadius);
        minSpeed = Mathf.Max(0f, minSpeed);
        maxSpeed = Mathf.Max(minSpeed, maxSpeed);
        maxForce = Mathf.Max(0.01f, maxForce);
        predatorFraction = Mathf.Clamp(predatorFraction, 0f, 0.2f);
        preyFleeRadius = Mathf.Max(0.01f, preyFleeRadius);
        predatorHuntRadius = Mathf.Max(0.01f, predatorHuntRadius);
        predatorSpeedMultiplier = Mathf.Max(0.1f, predatorSpeedMultiplier);
        predatorForceMultiplier = Mathf.Max(0.1f, predatorForceMultiplier);
        maxEnvironmentAcceleration = Mathf.Max(0.01f, maxEnvironmentAcceleration);
        maxForceZones = Mathf.Max(1, maxForceZones);
        maxObstacles = Mathf.Max(1, maxObstacles);
    }

    private bool TryCreateResources()
    {
        if (boidsCompute == null ||
            visualEffect == null ||
            visualEffect.visualEffectAsset == null)
        {
            if (!_warnedAboutMissingReferences)
            {
                Debug.LogWarning(
                    $"{nameof(FlockManager)} needs a Compute Shader and a Visual Effect component " +
                    "with a VFX Graph asset.",
                    this);
                _warnedAboutMissingReferences = true;
            }

            return false;
        }

        _warnedAboutMissingReferences = false;

        if (!_kernelsFound)
        {
            _initializeKernel = boidsCompute.FindKernel("InitializeBoids");
            _clearGridKernel = boidsCompute.FindKernel("ClearGrid");
            _buildGridKernel = boidsCompute.FindKernel("BuildGrid");
            _simulateKernel = boidsCompute.FindKernel("SimulateBoids");
            _kernelsFound = true;
        }

        Vector3Int requiredDimensions = CalculateGridDimensions();
        bool configurationChanged =
            _boidsA == null ||
            _boidsB == null ||
            _allocatedBoidCount != boidCount ||
            _allocatedMaxBoidsPerCell != maxBoidsPerCell ||
            !Mathf.Approximately(_allocatedCellSize, cellSize) ||
            _allocatedBounds != simulationBounds ||
            _allocatedRandomSeed != randomSeed ||
            !Mathf.Approximately(_allocatedPredatorFraction, predatorFraction) ||
            _allocatedMaxForceZones != maxForceZones ||
            _allocatedMaxObstacles != maxObstacles ||
            _gridDimensions != requiredDimensions;

        if (!configurationChanged)
        {
            return true;
        }

        ReleaseResources();
        AllocateResources(requiredDimensions);
        InitializeBoids();
        visualEffect.Reinit();
        PublishCurrentBuffer();
        return true;
    }

    private Vector3Int CalculateGridDimensions()
    {
        return new Vector3Int(
            Mathf.Max(1, Mathf.CeilToInt(simulationBounds.x / cellSize)),
            Mathf.Max(1, Mathf.CeilToInt(simulationBounds.y / cellSize)),
            Mathf.Max(1, Mathf.CeilToInt(simulationBounds.z / cellSize)));
    }

    private void AllocateResources(Vector3Int dimensions)
    {
        long cellCount = (long)dimensions.x * dimensions.y * dimensions.z;
        long indexCount = cellCount * maxBoidsPerCell;

        if (cellCount > int.MaxValue || indexCount > int.MaxValue)
        {
            throw new InvalidOperationException(
                "The voxel grid is too large. Increase Cell Size, reduce Simulation Bounds, " +
                "or reduce Max Boids Per Cell.");
        }

        _gridDimensions = dimensions;
        _gridCellCount = (int)cellCount;
        _allocatedBoidCount = boidCount;
        _allocatedMaxBoidsPerCell = maxBoidsPerCell;
        _allocatedCellSize = cellSize;
        _allocatedBounds = simulationBounds;
        _allocatedRandomSeed = randomSeed;
        _allocatedPredatorFraction = predatorFraction;
        _allocatedMaxForceZones = maxForceZones;
        _allocatedMaxObstacles = maxObstacles;

        _boidsA = CreateBuffer(boidCount, BoidStride, "Boids A");
        _boidsB = CreateBuffer(boidCount, BoidStride, "Boids B");
        _cellCounts = CreateBuffer(_gridCellCount, UIntStride, "Boid cell counts");
        _cellBoidIndices = CreateBuffer((int)indexCount, UIntStride, "Boid cell indices");
        _overflowCount = CreateBuffer(1, UIntStride, "Boid grid overflow");
        _forceZones = CreateBuffer(maxForceZones, ForceZoneStride, "Boid force zones");
        _obstacles = CreateBuffer(maxObstacles, ObstacleStride, "Boid obstacles");
        _forceZoneUpload = new BoidForceZone.GpuData[maxForceZones];
        _obstacleUpload = new BoidObstacle.GpuData[maxObstacles];
    }

    private static GraphicsBuffer CreateBuffer(int count, int stride, string bufferName)
    {
        GraphicsBuffer buffer = new(GraphicsBuffer.Target.Structured, count, stride)
        {
            name = bufferName
        };
        return buffer;
    }

    private void InitializeBoids()
    {
        UpdateShaderParameters();
        boidsCompute.SetInt(RandomSeedId, randomSeed);
        boidsCompute.SetBuffer(_initializeKernel, BoidsWriteId, _boidsA);
        boidsCompute.Dispatch(_initializeKernel, DivideRoundUp(boidCount, ThreadGroupSize), 1, 1);
        LastOverflowCount = 0;
    }

    private void UpdateShaderParameters()
    {
        int preyCellSearchRadius = RadiusToCellCount(
            Mathf.Max(Mathf.Max(neighborRadius, separationRadius), preyFleeRadius));
        int predatorCellSearchRadius = RadiusToCellCount(
            Mathf.Max(Mathf.Max(neighborRadius, separationRadius), predatorHuntRadius));

        boidsCompute.SetInt(BoidCountId, boidCount);
        boidsCompute.SetInt(GridCellCountId, _gridCellCount);
        boidsCompute.SetInts(
            GridDimensionsId,
            _gridDimensions.x,
            _gridDimensions.y,
            _gridDimensions.z);
        boidsCompute.SetInt(MaxBoidsPerCellId, maxBoidsPerCell);
        boidsCompute.SetInt(PreyCellSearchRadiusId, preyCellSearchRadius);
        boidsCompute.SetInt(PredatorCellSearchRadiusId, predatorCellSearchRadius);
        boidsCompute.SetVector(BoundsCenterId, transform.position);
        boidsCompute.SetVector(BoundsSizeId, simulationBounds);
        boidsCompute.SetFloat(CellSizeId, cellSize);
        boidsCompute.SetFloat(DeltaTimeId, Mathf.Min(Time.deltaTime, maximumDeltaTime));
        boidsCompute.SetFloat(NeighborRadiusId, neighborRadius);
        boidsCompute.SetFloat(SeparationRadiusId, separationRadius);
        boidsCompute.SetFloat(CohesionWeightId, cohesionWeight);
        boidsCompute.SetFloat(SeparationWeightId, separationWeight);
        boidsCompute.SetFloat(AlignmentWeightId, alignmentWeight);
        boidsCompute.SetFloat(BoundaryWeightId, boundaryWeight);
        boidsCompute.SetFloat(MinSpeedId, minSpeed);
        boidsCompute.SetFloat(MaxSpeedId, maxSpeed);
        boidsCompute.SetFloat(MaxForceId, maxForce);
        boidsCompute.SetFloat(PredatorFractionId, predatorFraction);
        boidsCompute.SetFloat(PreyFleeRadiusId, preyFleeRadius);
        boidsCompute.SetFloat(PreyFleeWeightId, preyFleeWeight);
        boidsCompute.SetFloat(PredatorHuntRadiusId, predatorHuntRadius);
        boidsCompute.SetFloat(PredatorChaseWeightId, predatorChaseWeight);
        boidsCompute.SetFloat(PredatorSpeedMultiplierId, predatorSpeedMultiplier);
        boidsCompute.SetFloat(PredatorForceMultiplierId, predatorForceMultiplier);
        boidsCompute.SetFloat(PredatorFlockingMultiplierId, predatorFlockingMultiplier);
        boidsCompute.SetInt(ForceZoneCountId, _activeForceZoneCount);
        boidsCompute.SetInt(ObstacleCountId, _activeObstacleCount);
        boidsCompute.SetFloat(SimulationTimeId, Time.time);
        boidsCompute.SetFloat(MaxEnvironmentAccelerationId, maxEnvironmentAcceleration);
    }

    private void UploadSceneInfluences()
    {
        _activeForceZoneCount = 0;
        IReadOnlyList<BoidForceZone> activeZones = BoidForceZone.Active;
        for (int i = 0; i < activeZones.Count && _activeForceZoneCount < maxForceZones; i++)
        {
            BoidForceZone zone = activeZones[i];
            if (zone != null && zone.TryGetGpuData(out BoidForceZone.GpuData data))
            {
                _forceZoneUpload[_activeForceZoneCount++] = data;
            }
        }

        bool forceZoneOverflow = activeZones.Count > maxForceZones;
        if (forceZoneOverflow && !_warnedAboutForceZoneCapacity)
        {
            Debug.LogWarning(
                $"Only the first {maxForceZones} active BoidForceZone components are sent to the GPU. " +
                "Increase Max Force Zones on FlockManager.",
                this);
        }
        _warnedAboutForceZoneCapacity = forceZoneOverflow;

        if (_activeForceZoneCount > 0)
        {
            _forceZones.SetData(_forceZoneUpload, 0, 0, _activeForceZoneCount);
        }

        _activeObstacleCount = 0;
        IReadOnlyList<BoidObstacle> activeObstacles = BoidObstacle.Active;
        for (int i = 0; i < activeObstacles.Count && _activeObstacleCount < maxObstacles; i++)
        {
            BoidObstacle obstacle = activeObstacles[i];
            if (obstacle != null && obstacle.TryGetGpuData(out BoidObstacle.GpuData data))
            {
                _obstacleUpload[_activeObstacleCount++] = data;
            }
        }

        bool obstacleOverflow = activeObstacles.Count > maxObstacles;
        if (obstacleOverflow && !_warnedAboutObstacleCapacity)
        {
            Debug.LogWarning(
                $"Only the first {maxObstacles} active BoidObstacle components are sent to the GPU. " +
                "Increase Max Obstacles on FlockManager.",
                this);
        }
        _warnedAboutObstacleCapacity = obstacleOverflow;

        if (_activeObstacleCount > 0)
        {
            _obstacles.SetData(_obstacleUpload, 0, 0, _activeObstacleCount);
        }

        boidsCompute.SetInt(ForceZoneCountId, _activeForceZoneCount);
        boidsCompute.SetInt(ObstacleCountId, _activeObstacleCount);
    }

    private void ClearAndBuildGrid()
    {
        boidsCompute.SetBuffer(_clearGridKernel, CellCountsId, _cellCounts);
        boidsCompute.SetBuffer(_clearGridKernel, OverflowCountId, _overflowCount);
        boidsCompute.Dispatch(
            _clearGridKernel,
            DivideRoundUp(_gridCellCount, ThreadGroupSize),
            1,
            1);

        boidsCompute.SetBuffer(_buildGridKernel, BoidsReadId, _boidsA);
        boidsCompute.SetBuffer(_buildGridKernel, CellCountsId, _cellCounts);
        boidsCompute.SetBuffer(_buildGridKernel, CellBoidIndicesId, _cellBoidIndices);
        boidsCompute.SetBuffer(_buildGridKernel, OverflowCountId, _overflowCount);
        boidsCompute.Dispatch(
            _buildGridKernel,
            DivideRoundUp(boidCount, ThreadGroupSize),
            1,
            1);
    }

    private void Simulate()
    {
        boidsCompute.SetBuffer(_simulateKernel, BoidsReadId, _boidsA);
        boidsCompute.SetBuffer(_simulateKernel, BoidsWriteId, _boidsB);
        boidsCompute.SetBuffer(_simulateKernel, CellCountsId, _cellCounts);
        boidsCompute.SetBuffer(_simulateKernel, CellBoidIndicesId, _cellBoidIndices);
        boidsCompute.SetBuffer(_simulateKernel, ForceZonesId, _forceZones);
        boidsCompute.SetBuffer(_simulateKernel, ObstaclesId, _obstacles);
        boidsCompute.Dispatch(
            _simulateKernel,
            DivideRoundUp(boidCount, ThreadGroupSize),
            1,
            1);

        (_boidsA, _boidsB) = (_boidsB, _boidsA);
    }

    private void PublishCurrentBuffer()
    {
        if (visualEffect == null || _boidsA == null)
        {
            return;
        }

        if (!visualEffect.HasGraphicsBuffer("BoidBuffer") ||
            !visualEffect.HasInt("BoidCount"))
        {
            if (!_warnedAboutMissingVfxProperties)
            {
                Debug.LogWarning(
                    "The VFX Graph must expose a Graphics Buffer named 'BoidBuffer' and an int " +
                    "named 'BoidCount'. See Assets/Boids/README.md.",
                    this);
                _warnedAboutMissingVfxProperties = true;
            }

            return;
        }

        _warnedAboutMissingVfxProperties = false;
        visualEffect.SetGraphicsBuffer("BoidBuffer", _boidsA);
        visualEffect.SetInt("BoidCount", boidCount);
    }

    private void RequestOverflowReadbackIfNeeded()
    {
        if (!monitorGridOverflow ||
            _overflowReadbackPending ||
            Time.unscaledTime < _nextOverflowCheckTime)
        {
            return;
        }

        _nextOverflowCheckTime = Time.unscaledTime + overflowCheckInterval;
        _overflowReadbackPending = true;

        AsyncGPUReadback.Request(_overflowCount, request =>
        {
            if (this == null)
            {
                return;
            }

            _overflowReadbackPending = false;
            if (request.hasError)
            {
                return;
            }

            LastOverflowCount = request.GetData<uint>()[0];
            if (LastOverflowCount > 0)
            {
                Debug.LogWarning(
                    $"The boid voxel grid dropped {LastOverflowCount} cell entries during the " +
                    "sampled frame. Increase Max Boids Per Cell, increase Cell Size, or enlarge " +
                    "the simulation bounds.",
                    this);
            }
        });
    }

    private void ReleaseResources()
    {
        ReleaseBuffer(ref _boidsA);
        ReleaseBuffer(ref _boidsB);
        ReleaseBuffer(ref _cellCounts);
        ReleaseBuffer(ref _cellBoidIndices);
        ReleaseBuffer(ref _overflowCount);
        ReleaseBuffer(ref _forceZones);
        ReleaseBuffer(ref _obstacles);

        _forceZoneUpload = null;
        _obstacleUpload = null;
        _activeForceZoneCount = 0;
        _activeObstacleCount = 0;

        _allocatedBoidCount = 0;
        _gridCellCount = 0;
        _overflowReadbackPending = false;
    }

    private static void ReleaseBuffer(ref GraphicsBuffer buffer)
    {
        buffer?.Release();
        buffer = null;
    }

    private static int DivideRoundUp(int value, int divisor)
    {
        return (value + divisor - 1) / divisor;
    }

    private int RadiusToCellCount(float radius)
    {
        return Mathf.Max(1, Mathf.CeilToInt(radius / cellSize));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.7f);
        Gizmos.DrawWireCube(transform.position, simulationBounds);
    }
}
