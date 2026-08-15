using Cysharp.Threading.Tasks;
using LitMotion;
using MessagePipe;
using Scellecs.Morpeh;
using UnityEngine;
using VContainer;

public sealed class CapacityBoosterSystem : SystemBase
{
    private Filter _filterDisabled;
    private Stash<BehaviourView<Carrier>> _carriers;

    [Inject] private PrefabModule _prefabModule;
    [Inject] private Particles _particles;

    [Inject] private IPublisher<CameraUpdateBoundsMessage> _cameraUpdateBoundsPub;

    public override void OnAwake()
    {
        base.OnAwake();

        _filterDisabled = World.Filter.With<BehaviourView<Carrier>>().Without<Enabled>().Build();
        _carriers = World.GetStash<BehaviourView<Carrier>>();
    }

    public bool HasCapacityCarrier()
    {
        return _filterDisabled.IsNotEmpty();
    }

    public async UniTaskVoid IncreaseCapacity()
    {
        Carrier spawnedCarrier = null;
        foreach (var entity in _filterDisabled)
        {
            Carrier carrier = _carriers.Get(entity);
            if (spawnedCarrier == null || carrier.Type == CarrierSheet.CarrierType.Y)
                spawnedCarrier = carrier;
        }

        if (spawnedCarrier == null) return;
        spawnedCarrier.gameObject.SetActive(true);

        World.Commit();
        _cameraUpdateBoundsPub.Publish(new CameraUpdateBoundsMessage());

        var spawnedCarrierT = spawnedCarrier.transform;
        var center = spawnedCarrierT.position;

        LMotion.Create(Vector3H.Half, Vector3.one, .4f)
            .WithEase(Ease.InOutBack)
            .BindToLocalScaleAround(spawnedCarrierT, center)
            .AddTo(spawnedCarrier);

        await UniTask.Delay(100, cancellationToken: SceneLoadToken);

        _prefabModule.Rent(_particles.CarrierSpawn, center, spawnedCarrierT.rotation);
    }
}