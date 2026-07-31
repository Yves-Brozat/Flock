# Mise en place du test GPU Boids

La simulation du flock s'exécute entièrement sur le GPU. Le script C# lance
les kernels du compute shader et transmet le `GraphicsBuffer` courant au VFX
Graph. Les positions des boids ne sont jamais recopiées vers le CPU.

## Mise en place de la scène

1. Créer un GameObject vide nommé `GPU Flock`.
2. Lui ajouter un composant `Visual Effect`.
3. Lui ajouter le composant `FlockManager`.
4. Assigner `Assets/Shaders/Boids.compute` à **Boids Compute**.
5. Assigner le composant `Visual Effect` à **Visual Effect**.
6. Créer le VFX Graph décrit ci-dessous et l'assigner au Visual Effect.

Commencer avec 32 768 boids. Une fois l'effet fonctionnel, essayer 65 536 puis
131 072 boids et utiliser le GPU Profiler pour choisir une population adaptée
à la machine cible.

## Construction du VFX Graph

Créer un VFX Graph nommé `BoidsVFX`.

### Blackboard

Créer et exposer les deux propriétés suivantes. Les majuscules sont
significatives :

- `BoidBuffer`, de type **Graphics Buffer** ;
- `BoidCount`, de type **int**.

### Spawn

Utiliser un unique burst dont le nombre de particules est `BoidCount`. Les
particules doivent être créées une fois et non continuellement.

### Initialize Particle

- Définir une durée de vie très longue.
- Définir une petite taille, par exemple `0.15`.
- Augmenter la capacité du système pour qu'elle soit au moins égale au plus
  grand `BoidCount` prévu.
- Configurer des bounds fixes assez grands pour contenir la boîte dessinée par
  `FlockManager`.

### Update Particle

1. Ajouter l'opérateur **Sample Graphics Buffer**.
2. Connecter la propriété exposée `BoidBuffer` à son entrée Buffer.
3. Ouvrir les réglages de l'opérateur et sélectionner `BoidData` comme type de
   structure.
4. Convertir `particleId` en `uint` et l'utiliser comme Index.
5. Déplier le résultat échantillonné :
   - affecter `position.xyz` à la position de la particule ;
   - affecter `velocity.xyz` à sa vélocité ;
   - lire `position.w` pour distinguer les espèces : `0` désigne une proie
     et `1` un prédateur.

Ne pas ajouter de bloc qui intègre une nouvelle fois la vélocité dans la
position : le compute shader fournit déjà la position finale à chaque image.

### Output

Pour un test rapide, utiliser **Output Particle Quad** et orienter les
particules dans la direction de leur vélocité.

Pour un flock plus lisible, utiliser **Output Particle Mesh**, assigner un mesh
low-poly et l'orienter dans la direction de la vélocité. L'axe avant du mesh
doit correspondre à celui utilisé par le bloc d'orientation du VFX Graph.

Pour visualiser les prédateurs, utiliser `position.w` comme interpolation
entre une couleur de proie et une couleur de prédateur. La même valeur peut
servir à interpoler la taille ou sélectionner un autre mesh.

## Comprendre les paramètres de mouvement

`Max Speed` est une vitesse en unités par seconde. `Max Force` est ici une
accélération maximale en unités par seconde carrée :

- une faible Max Force produit de grands virages doux ;
- une forte Max Force permet des changements de direction brusques ;
- à vitesse égale, doubler Max Force réduit approximativement le rayon de
  virage de moitié.

Les poids de cohésion, séparation, alignement, fuite et poursuite sont
additionnés avant que le résultat soit limité par Max Force. Quand plusieurs
forces saturent déjà cette limite, augmenter un poids change surtout la
priorité du comportement correspondant. Cela n'augmente plus l'accélération
totale.

`Maximum Delta Time` n'est pas une force. Il limite la durée simulée par image
pour éviter un bond après une image très lente :

- `0.0167` limite le pas à environ 1/60 seconde ;
- `0.0333` limite le pas à environ 1/30 seconde ;
- si le jeu tourne plus lentement que cette limite, le flock passe au ralenti
  au lieu de devenir instable.

Méthode de réglage conseillée :

1. Désactiver temporairement les prédateurs avec `Predator Fraction = 0`.
2. Mettre Cohesion à `0.6`, Alignment à `1`, Separation à `1.5`.
3. Régler Max Speed, puis Max Force jusqu'à obtenir le rayon de virage voulu.
4. Ajuster Separation Radius avant de modifier fortement Separation Weight.
5. Réintroduire les prédateurs et régler Flee/Chase en dernier.

## Proies et prédateurs

Les deux espèces partagent la même grille de voxels :

- la cohésion et l'alignement ne considèrent que les boids de la même espèce ;
- la séparation considère tous les boids ;
- une proie fuit tous les prédateurs contenus dans `Prey Flee Radius` ;
- un prédateur poursuit la proie la plus proche dans `Predator Hunt Radius`.

Réglages de départ :

- Predator Fraction = `0.005` à `0.01` ;
- Prey Flee Radius = `6`, Prey Flee Weight = `2.5` ;
- Predator Hunt Radius = `12`, Predator Chase Weight = `1.5` ;
- Predator Speed Multiplier = `1.25` ;
- Predator Force Multiplier = `1.5` ;
- Predator Flocking Multiplier = `0.25`.

La fraction et le Random Seed sont appliqués à l'initialisation. Les modifier
en Play Mode recrée la population. Les autres réglages sont modifiables en
temps réel.

Cette première version simule la poursuite et la fuite, mais pas encore la
capture ou la disparition des proies.

## Réglages de la grille de voxels

`Cell Size` définit la taille d'un voxel. Le compute shader vérifie
automatiquement assez de cellules pour couvrir `Neighbor Radius`.

Réglages de départ conseillés :

- Cell Size = Neighbor Radius ;
- Max Boids Per Cell = 64 ;
- Separation Radius inférieur à Neighbor Radius.

Le compteur de débordement est lu de manière asynchrone. Si la Console signale
un débordement, certains voisins d'une cellule trop chargée ont été omis
pendant cette image. Augmenter `Max Boids Per Cell`, augmenter `Cell Size` ou
agrandir les bounds de simulation.

La mémoire du buffer d'indices est approximativement :

`nombreDeCellules * Max Boids Per Cell * 4 octets`

Des cellules minuscules réparties sur un très grand volume peuvent donc
consommer davantage de mémoire qu'une grille plus grossière.

## Zones de forces environnementales

Une zone est ici une sphère d'influence centrée sur un GameObject. Elle ne
correspond pas à un volume HDRP.

Créer une zone avec `GameObject > Boids > Force Zone`, puis choisir son type :

- **Curl Noise** : turbulence continue animée. `Noise Scale` contrôle la taille
  des tourbillons et `Noise Speed` leur animation ;
- **Directional Current** : accélération dans `Local Direction`. La direction
  suit la rotation du GameObject. `(0,-1,0)` peut servir de gravité locale ;
- **Vortex** : rotation tangentielle autour de l'axe local X, Y ou Z. Une
  `Strength` négative inverse la rotation et `Vortex Inward Strength` attire
  vers l'axe pour produire une spirale ;
- **Radial** : force dirigée selon le rayon entre le centre de la zone et le
  boid. Une `Strength` positive repousse, une valeur négative attire.

Paramètres communs :

- `Radius` : rayon de la sphère d'influence ;
- `Strength` : accélération maximale au centre ;
- `Falloff Exponent` : profil d'atténuation. `1` est linéaire, une valeur
  supérieure concentre la force près du centre.

Une force radiale pointe directement vers le centre ou vers l'extérieur. Un
vortex est perpendiculaire au rayon : il fait tourner autour d'un axe. Le
vortex dispose aussi d'une attraction vers son axe pour produire une spirale.

La somme des zones est limitée par `Max Environment Acceleration` sur le
`FlockManager`. Ce budget est distinct de `Max Force`, afin qu'un courant ne
supprime pas la capacité du boid à maintenir son flock.

## Obstacles analytiques

Créer un obstacle avec `GameObject > Boids > Obstacle`, puis sélectionner :

- **Sphere** avec `Sphere Radius` ;
- **Box** avec `Box Size` ;
- **Capsule** avec `Capsule Radius` et `Capsule Height`. Son axe est le Y local.

La position, la rotation et l'échelle du GameObject sont prises en compte. Un
Collider Unity n'est pas nécessaire : le composant envoie directement la forme
analytique au compute shader.

- `Avoidance Distance` définit la marge avant la surface ;
- `Avoidance Strength` donne la priorité d'évitement ;
- `Look Ahead Time` teste la position future du boid. Commencer entre `0.2` et
  `0.5` seconde.

Les gizmos rouges montrent les formes réellement envoyées au GPU. Si les boids
entrent dans un obstacle, augmenter d'abord `Look Ahead Time`, puis
`Avoidance Distance` et enfin `Avoidance Strength`.

## Ondulation visuelle des traînées

Le fichier `Assets/Shaders/BoidTrailUndulation.hlsl` contient un bloc HLSL
réutilisable. Il ne faut l'ajouter qu'au **Update** du système de traînées, pas
au Update des têtes de boids dont la position vient du Graphics Buffer.

1. Dans le Update des traînées, ajouter `HLSL > Custom HLSL`.
2. Dans l'Inspector du bloc, sélectionner la source **File** et assigner
   `BoidTrailUndulation.hlsl`.
3. Sélectionner la fonction `ApplyTrailUndulation`.
4. Connecter un opérateur **Total Time** à `currentTime`.
5. Connecter un opérateur **Delta Time** à `deltaTime`.
6. Commencer avec :
   - Amplitude = `0.08` ;
   - Frequency = `8` ;
   - Animation Speed = `3`.
7. Placer le bloc après les autres blocs qui écrivent la position de la
   traînée.

L'ondulation est nulle à la tête et à la fin de la traînée, maximale vers son
milieu, et utilise une phase différente par particule. Elle reste visuelle et
ne modifie pas la simulation des boids.
