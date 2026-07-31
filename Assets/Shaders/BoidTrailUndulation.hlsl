#ifndef BOID_TRAIL_UNDULATION_INCLUDED
#define BOID_TRAIL_UNDULATION_INCLUDED

/// amplitude: Lateral displacement in system units.
/// frequency: Oscillation frequency along particle age, in radians per second.
/// animationSpeed: Global phase animation speed, in radians per second.
/// currentTime: Connect a Total Time operator.
/// deltaTime: Connect a Delta Time operator.
void ApplyTrailUndulation(
    inout VFXAttributes attributes,
    in float amplitude,
    in float frequency,
    in float animationSpeed,
    in float currentTime,
    in float deltaTime)
{
    if (amplitude == 0.0)
    {
        return;
    }

    float velocityLengthSquared = dot(attributes.velocity, attributes.velocity);
    float3 forward = velocityLengthSquared > 1e-8
        ? attributes.velocity * rsqrt(velocityLengthSquared)
        : float3(0.0, 0.0, 1.0);
    float3 referenceAxis = abs(forward.y) < 0.95
        ? float3(0.0, 1.0, 0.0)
        : float3(1.0, 0.0, 0.0);
    float3 side = normalize(cross(forward, referenceAxis));

    float lifetime = max(attributes.lifetime, 1e-4);
    float previousAge = max(0.0, attributes.age - deltaTime);
    float currentEnvelope = sin(saturate(attributes.age / lifetime) * 3.14159265);
    float previousEnvelope = sin(saturate(previousAge / lifetime) * 3.14159265);

    // Every point of one strip must share the same phase offset. Using
    // particleId here would make adjacent points jump in unrelated directions.
    float stripPhase = (float)(attributes.stripIndex & 1023u) * 0.61803399;
    float currentPhase =
        attributes.age * frequency +
        currentTime * animationSpeed +
        stripPhase;
    float previousPhase =
        previousAge * frequency +
        (currentTime - deltaTime) * animationSpeed +
        stripPhase;

    float currentOffset = sin(currentPhase) * amplitude * currentEnvelope;
    float previousOffset = sin(previousPhase) * amplitude * previousEnvelope;
    attributes.position += side * (currentOffset - previousOffset);
}

#endif
