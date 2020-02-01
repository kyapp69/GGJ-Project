using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using AudioVisualizer;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public unsafe class MusicRing : MonoBehaviour
{
    Mesh ringMesh = null;
    public int ringElement = 12;
    public float ringSize = 10;
    public float ringWidth = 1;
    public Material lineMat;
    public Material triangleMat;
    public AudioSampler audioSampler;
    public float ringPower = 2;
    private Transform pointer;
    private List<Vector3> positions;
    private AnimationCurve[] curves;

    [Tooltip("Index into the AudioSampler audioSources or audioFiles list")]
    public int audioIndex = 0; // index into audioSampler audioSources or audioFIles list. Determines which audio source we want to sample
    [Tooltip("How sensitive to the audio are we?")]
    public float sensitivity = 2; // how sensitive is this script to the audio. This value is multiplied by the audio sample data.
    [Tooltip("The sprite to be used in each cell")]
    public Sprite sprite; // the sprite used for each cell of the waveform
    [Tooltip("The angle of the arc, max = 360")]
    [Range(0, 360)]
    public float angle = 180;
    [Tooltip("The radius of the arc")]
    public float radius = 10f;
    [Tooltip("The height of the waveform \n effects the height of each sprite")]
    public float height = 5f;
    [Tooltip("How fast the waveform moves")]
    public float lerpSpeed = 20f; // how fast the panel is updated

    private void Awake()
    {
        ringMesh = new Mesh();
        pointer = new GameObject("Pointer").transform;
        positions = new List<Vector3>(ringElement * 4);
        pointer.position = Vector3.zero;
        pointer.localScale = Vector3.one;
        pointer.rotation = Quaternion.identity;
        List<int> indices = new List<int>(ringElement * 8);
        List<int> triangleIndices = new List<int>(ringElement * 6);

        for (int i = 0, ind = 0; i < ringElement; ++i)
        {
            float lastEuler = (float)i / ringElement * 360f;
            float nextEuler = (float)(i + 1f) / ringElement * 360f;
            pointer.eulerAngles = float3(lastEuler, 0, 0);
            float3 lastInside = (float3)pointer.forward * ringSize;
            float3 lastOutside = (float3)pointer.forward * (ringSize + ringWidth);
            pointer.eulerAngles = float3(nextEuler, 0, 0);
            float3 nextInside = (float3)pointer.forward * ringSize;
            float3 nextOutside = (float3)pointer.forward * (ringSize + ringWidth);
            positions.Add(lastInside);
            positions.Add(lastOutside);
            positions.Add(nextInside);
            positions.Add(nextOutside);
            indices.Add(ind);
            indices.Add(ind + 1);
            indices.Add(ind);
            indices.Add(ind + 2);
            indices.Add(ind + 1);
            indices.Add(ind + 3);

            triangleIndices.Add(ind);
            triangleIndices.Add(ind + 1);
            triangleIndices.Add(ind + 2);
            triangleIndices.Add(ind + 3);
            triangleIndices.Add(ind + 2);
            triangleIndices.Add(ind + 1);
            ind += 4;
        }
        ringMesh.subMeshCount = 2;
        ringMesh.SetVertices(positions);
        ringMesh.SetIndices(indices, MeshTopology.Lines, 0);
        ringMesh.SetIndices(triangleIndices, MeshTopology.Triangles, 1);
        GetComponent<MeshFilter>().sharedMesh = ringMesh;
        GetComponent<MeshRenderer>().sharedMaterials = new Material[]{
            lineMat,
            triangleMat
        };
        curves = new AnimationCurve[ringElement];
        const int v = 10;
        Keyframe[] keyframes = new Keyframe[v];
        float* randVs = stackalloc float[v];
        for (int j = 0; j < v; ++j)
        {
            randVs[j] = UnityEngine.Random.Range(0f, 1f);
        }

        for (int i = 0; i < ringElement; ++i)
        {
            pointer.eulerAngles = float3((float)i / (ringElement) * 360, 0, 0);
            float dotV = dot(pointer.forward, float3(0, 1, 0));
            dotV = pow(saturate(dotV), ringPower);
            float rand = UnityEngine.Random.Range(0.1f, 0.9f);
            for (int j = 0; j < v; ++j)
            {
                randVs[j] = UnityEngine.Random.Range(0f, 1f);
                keyframes[j] = new Keyframe(j / (v - 1f), randVs[j] * dotV);
            }
            curves[i] = new AnimationCurve(keyframes);
        }
    }
    public void SetVertex(int i, float width)
    {
        float lastEuler = (float)i / ringElement * 360f;
        float nextEuler = (float)(i + 1f) / ringElement * 360f;
        pointer.eulerAngles = float3(lastEuler, 0, 0);
        float3 lastInside = (float3)pointer.forward * (ringSize - width);
        float3 lastOutside = (float3)pointer.forward * (ringSize + ringWidth + width);
        pointer.eulerAngles = float3(nextEuler, 0, 0);
        float3 nextInside = (float3)pointer.forward * (ringSize - width);
        float3 nextOutside = (float3)pointer.forward * (ringSize + ringWidth + width);
        int nextI = (i + 1 >= ringElement) ? 0 : i + 1;
        int lastI = (i - 1 < 0 ? ringElement - 1 : i - 1);
        positions[i * 4] = lastInside;
        positions[lastI * 4 + 2] = lastInside;
        positions[i * 4 + 1] = lastOutside;
        positions[lastI * 4 + 3] = lastOutside;
        positions[i * 4 + 2] = nextInside;
        positions[nextI * 4] = nextInside;
        positions[i * 4 + 3] = nextOutside;
        positions[nextI * 4 + 1] = nextOutside;
    }

    public void LateUpdate()
    {
        DrawWaveform();
        ringMesh.SetVertices(positions);
        ringMesh.UploadMeshData(false);
    }
    float[] samples = null;
    void DrawWaveform()
    {
        float[] audioSamples;
        audioSamples = AudioSampler.instance.GetAudioSamples(audioIndex, ringElement, true, false);
        if (samples == null || samples.Length != audioSamples.Length)
        {
            samples = new float[audioSamples.Length];
        }

        float delta = Time.deltaTime * lerpSpeed;
        for (int i = 0; i < ringElement; i++)
        {
            if (!(audioSamples[i] > 0) && !(audioSamples[i] < 0))
            {
                audioSamples[i] = 0;
            }
            audioSamples[i] = curves[i].Evaluate(audioSamples[i]);
            samples[i] = Mathf.Lerp(samples[i], audioSamples[i], delta);
            if (!(samples[i] > 0) && !(samples[i] < 0))
            {
                samples[i] = 0;
            }
        }

        for (int c = 0; c < ringElement; c++)
        {
            //top and bottom....
            float sample = samples[c];
            float sampleHeight = Mathf.Abs(sample) * sensitivity * AudioSampler.instance.globalSensitivity; //get an audio sample for each column
            SetVertex(c, sampleHeight);
        }
    }
}
