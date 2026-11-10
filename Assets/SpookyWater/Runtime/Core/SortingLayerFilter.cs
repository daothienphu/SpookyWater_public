using System;
using UnityEngine;

namespace SpookyWater
{
    [Serializable]
    public struct SortingLayerFilter
    {
        [SerializeField] private string[] _excluded;

        public static SortingLayerFilter Everything => new SortingLayerFilter { _excluded = Array.Empty<string>() };

        public bool IsEverything => _excluded == null || _excluded.Length == 0;

        public bool Includes(string layerName)
        {
            if (_excluded == null) return true;
            for (var i = 0; i < _excluded.Length; i++)
            {
                if (_excluded[i] == layerName) return false;
            }
            return true;
        }

        public bool Includes(int sortingLayerId)
        {
            return Includes(SortingLayer.IDToName(sortingLayerId));
        }

        public void SetIncluded(string layerName, bool included)
        {
            if (string.IsNullOrEmpty(layerName) || Includes(layerName) == included) return;
            if (included)
            {
                _excluded = Array.FindAll(_excluded, n => n != layerName);
                return;
            }
            var list = _excluded ?? Array.Empty<string>();
            Array.Resize(ref list, list.Length + 1);
            list[list.Length - 1] = layerName;
            _excluded = list;
        }
    }
}
