using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sound;
using UnityEngine;

public class SoundManager : SerializedMonoBehaviour
{
    [SerializeField] private Dictionary<SoundType, AudioClip[]> sounds;
    [SerializeField] private AudioSource sourcePrefab;

    private readonly List<AudioSource> sourcePool = new();
    private readonly List<AudioSource> sourceInUse = new();
    private readonly List<AudioSource> swapper = new();

    public void Play(SoundType soundType, Vector3 position, float pitch)
    {
        var collection = sounds[soundType];
        var sound = collection[Random.Range(0, collection.Length)];

        var source = GetSource();
        source.transform.position = position;
        source.pitch = pitch;
        source.PlayOneShot(sound);
    }

    private AudioSource GetSource()
    {
        if (sourcePool.Count == 0)
        {
            var newSource = Instantiate(sourcePrefab, transform);
            sourceInUse.Add(newSource);
            return newSource;
        }

        var source = sourcePool.First();
        sourcePool.Remove(source);
        sourceInUse.Add(source);
        return source;
    }

    private void Update()
    {
        foreach (var source in sourceInUse)
        {
            if (!source.isPlaying)
                swapper.Add(source);
        }

        foreach (var source in swapper)
        {
            sourceInUse.Remove(source);
            sourcePool.Add(source);
        }
        
        swapper.Clear();
    }
}
