using UnityEditor;
using UnityEngine;

public static class BoidSceneMenu
{
    [MenuItem("GameObject/Boids/Force Zone", false, 10)]
    private static void CreateForceZone(MenuCommand menuCommand)
    {
        GameObject gameObject = new("Boid Force Zone");
        GameObjectUtility.SetParentAndAlign(gameObject, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(gameObject, "Create Boid Force Zone");
        gameObject.AddComponent<BoidForceZone>();
        Selection.activeGameObject = gameObject;
    }

    [MenuItem("GameObject/Boids/Obstacle", false, 11)]
    private static void CreateObstacle(MenuCommand menuCommand)
    {
        GameObject gameObject = new("Boid Obstacle");
        GameObjectUtility.SetParentAndAlign(gameObject, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(gameObject, "Create Boid Obstacle");
        gameObject.AddComponent<BoidObstacle>();
        Selection.activeGameObject = gameObject;
    }
}
