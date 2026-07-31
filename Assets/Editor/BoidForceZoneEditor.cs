using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BoidForceZone))]
public sealed class BoidForceZoneEditor : Editor
{
    private SerializedProperty _forceType;
    private SerializedProperty _radius;
    private SerializedProperty _falloffExponent;
    private SerializedProperty _strength;
    private SerializedProperty _localDirection;
    private SerializedProperty _noiseScale;
    private SerializedProperty _noiseSpeed;
    private SerializedProperty _vortexAxis;
    private SerializedProperty _vortexInwardStrength;

    private void OnEnable()
    {
        _forceType = serializedObject.FindProperty("forceType");
        _radius = serializedObject.FindProperty("radius");
        _falloffExponent = serializedObject.FindProperty("falloffExponent");
        _strength = serializedObject.FindProperty("strength");
        _localDirection = serializedObject.FindProperty("localDirection");
        _noiseScale = serializedObject.FindProperty("noiseScale");
        _noiseSpeed = serializedObject.FindProperty("noiseSpeed");
        _vortexAxis = serializedObject.FindProperty("vortexAxis");
        _vortexInwardStrength = serializedObject.FindProperty("vortexInwardStrength");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_forceType);
        EditorGUILayout.PropertyField(_radius);
        EditorGUILayout.PropertyField(_falloffExponent);
        EditorGUILayout.PropertyField(_strength);
        EditorGUILayout.Space();

        BoidForceZone.ForceType type = (BoidForceZone.ForceType)_forceType.enumValueIndex;
        switch (type)
        {
            case BoidForceZone.ForceType.CurlNoise:
                EditorGUILayout.HelpBox(
                    "Champ turbulent continu et sans divergence, animé dans la sphère d'influence.",
                    MessageType.Info);
                EditorGUILayout.PropertyField(_noiseScale);
                EditorGUILayout.PropertyField(_noiseSpeed);
                break;

            case BoidForceZone.ForceType.DirectionalCurrent:
                EditorGUILayout.HelpBox(
                    "Accélération dans une direction locale. Utilise (0,-1,0) comme gravité locale.",
                    MessageType.Info);
                EditorGUILayout.PropertyField(_localDirection);
                break;

            case BoidForceZone.ForceType.Vortex:
                EditorGUILayout.HelpBox(
                    "Force tangentielle autour d'un axe. Strength négatif inverse le sens de rotation.",
                    MessageType.Info);
                EditorGUILayout.PropertyField(_vortexAxis);
                EditorGUILayout.PropertyField(_vortexInwardStrength);
                break;

            case BoidForceZone.ForceType.Radial:
                EditorGUILayout.HelpBox(
                    "Force dirigee depuis le centre. Strength positif repousse les boids, Strength negatif les attire.",
                    MessageType.Info);
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
