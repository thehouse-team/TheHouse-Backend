namespace TheHouse.Domain.Entities;

// NV-14: colonia o localidad (seed desde OpenStreetMap).
public sealed record Place(
    string Id,
    string Name,
    string Type,
    string City,
    double Latitude,
    double Longitude,
    long OsmNodeId);
