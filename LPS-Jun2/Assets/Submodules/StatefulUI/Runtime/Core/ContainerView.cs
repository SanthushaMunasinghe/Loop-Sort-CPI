using System;
using System.Collections.Generic;
using UnityEngine;

namespace StatefulUI.Runtime.Core
{
    public class ContainerView : MonoBehaviour
    {
        public RectTransform RectTransform => transform as RectTransform;

        [ProjectAssetOnly] public GameObject Prefab;

        [SerializeField] private Transform _root;

        public Transform Root => _root ? _root : transform;

        public List<GameObject> Instances { get; } = new List<GameObject>();

        public event Action OnAddTestItem = delegate { };
        public event Action OnClearTestItems = delegate { };

        public void AddTestItem()
        {
            if (Application.isPlaying)
            {
                AddInstance();
            }
            else
            {
                var instance = Instantiate(Prefab, Root);
                Instances.Add(instance);
            }

            OnAddTestItem?.Invoke();
        }

        public void ClearTransform()
        {
            foreach (var instance in Instances)
            {
                DestroyImmediate(instance.gameObject);
            }

            Instances.Clear();
            OnClearTestItems?.Invoke();
        }

        public void FillWithItems<TL>(IEnumerable<TL> items, Action<StatefulComponent, TL> action, bool keepItems = false)
        {
            if (!keepItems)
            {
                Clear();
            }

            foreach (var item in items)
            {
                var view = AddInstance().GetComponentAlways<StatefulComponent>();
                view.Localize();

                foreach (var innerComponent in view.InnerComponents)
                {
                    innerComponent.InnerComponent.Localize();
                }

                action(view, item);
            }
        }

        public void FillWithItems<TL>(Action<StatefulComponent, TL> action, bool keepItems = false, params TL[] items)
        {
            FillWithItems(items, action, keepItems);
        }

        public GameObject AddInstance()
        {
            var instance = Instances.Find(go => !go.activeSelf);
            if (instance == null)
            {
                instance = StatefulUiManager.Instance.InstantiatePrefab(Prefab);
                Instances.Add(instance);
            }

            instance.SetActive(true);
            var instanceTransform = instance.transform;
            instanceTransform.SetParent(Root);
            instanceTransform.localPosition = instanceTransform.localPosition.ChangeZ(0f);
            instanceTransform.localScale = Vector3.one;
            instanceTransform.localRotation = Quaternion.identity;

            return instance;
        }

        public T AddInstance<T>()
        {
            return AddInstance().GetComponent<T>();
        }

        public StatefulComponent AddStatefulComponent()
        {
            var view = AddInstance<StatefulComponent>();
            view.Localize();

            foreach (var innerComponent in view.InnerComponents)
            {
                innerComponent.InnerComponent.Localize();
            }

            return view;
        }

        public void Clear()
        {
            for (var i = Instances.Count - 1; i >= 0; i--)
            {
                var instance = Instances[i];
                if (instance == null)
                {
                    Instances.RemoveAt(i);
                    continue;
                }
                instance.SetActive(false);
            }
        }

        public void Remove(GameObject target, bool withDestroy = true)
        {
            if (withDestroy)
            {
                for (var i = Instances.Count - 1; i >= 0; i--)
                {
                    var instance = Instances[i];
                    if (instance != target) continue;
                    Instances.Remove(target);
                }
            }
            target.SetActive(false);
        }

        public void Restore(GameObject target)
        {
            Instances.Add(target);
            target.SetActive(true);
        }
    }
}