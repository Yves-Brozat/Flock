using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BoidObstacle))]
public sealed class BoidObstacleEditor : Editor
{
    private SerializedProperty _shape;
    private SerializedProperty _avoidanceDistance;
    private SerializedProperty _avoidanceStrength;
    private SerializedProperty _lookAheadTime;
    private SerializedProperty _sphereRadius;
    private SerializedProperty _boxSize;
    private SerializedProperty _capsuleRadius;
    private SerializedProperty _capsuleHeight;

    private void OnEnable()
    {
        _shape = serializedObject.FindProperty("shape");
        _avoidanceDistance = serializedObject.FindProperty("avoidanceDistance");
        _avoidanceStrength = serializedObject.FindProperty("avoidanceStrength");
        _lookAheadTime = serializedObject.FindProperty("lookAheadTime");
        _sphereRadius = serializedObject.FindProperty("sphereRadius");
        _boxSize = serializedObject.FindProperty("boxSize");
        _capsuleRadius = serializedObject.FindProperty("capsuleRadius");
        _capsuleHeight = serializedObject.FindProperty("capsuleHeight");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_shape);
        EditorGUILayout.PropertyField(_avoidanceDistance);
        EditorGUILayout.PropertyField(_avoidanceStrength);
        EditorGUILayout.PropertyField(_lookAheadTime);
        EditorGUILayout.Space();

        BoidObstacle.ObstacleShape shape = (BoidObstacle.ObstacleShape)_shape.enumValueIndex;
        switch (shape)
        {
            case BoidObstacle.ObstacleShape.Sphere:
                EditorGUILayout.PropertyField(_sphereRadius);
                break;
            case BoidObstacle.ObstacleShape.Box:
                EditorGUILayout.PropertyField(_boxSize);
                break;
            case BoidObstacle.ObstacleShape.Capsule:
                EditorGUILayout.HelpBox(
                    "La capsule est orientée le long de l'axe Y local du GameObject.",
                    MessageType.Info);
                EditorGUILayout.PropertyField(_capsuleRadius);
                EditorGUILayout.PropertyField(_capsuleHeight);
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
