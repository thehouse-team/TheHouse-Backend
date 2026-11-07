using TheHouse.Application.Common;
using TheHouse.Application.UseCases;

namespace TheHouse.UnitTests;

// NV-14: ubicación aproximada y seed de colonias.
public sealed class SearchTests
{
    [Theory]
    [InlineData(20.00903, -99.34471)]
    [InlineData(19.91703, -99.36116)]
    [InlineData(20.20551, -99.22178)]
    [InlineData(19.72791, -99.46312)]
    public void Cell_is_stable_and_close_to_the_real_point(double latitude, double longitude)
    {
        var (cellLat, cellLng) = ApproximateLocation.Cell(latitude, longitude);

        Assert.Equal((cellLat, cellLng), ApproximateLocation.Cell(latitude, longitude));
        Assert.False(cellLat == latitude && cellLng == longitude);
        Assert.True(ListingRules.DistanceKm(cellLat, cellLng, latitude, longitude) < 0.25);
    }

    [Fact]
    public void Points_in_the_same_cell_share_the_same_approximate_location()
    {
        // Dos edificios a pocos metros dentro de la misma celda no se distinguen.
        var first = ApproximateLocation.Cell(20.0101, -99.3401);
        var second = ApproximateLocation.Cell(20.0109, -99.3409);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Distances_from_several_universities_do_not_reveal_more_than_the_cell()
    {
        // Dos edificios distintos de la misma celda producen exactamente las
        // mismas distancias publicadas a cualquier universidad: triangular con
        // varias universidades no puede separarlos.
        var (aLat, aLng) = ApproximateLocation.Cell(20.0101, -99.3401);
        var (bLat, bLng) = ApproximateLocation.Cell(20.0109, -99.3409);
        var universities = new[] { (20.00903, -99.34471), (19.91703, -99.36116), (20.12991, -99.23041) };

        foreach (var (uLat, uLng) in universities)
        {
            Assert.Equal(
                Math.Round(ListingRules.DistanceKm(aLat, aLng, uLat, uLng), 1),
                Math.Round(ListingRules.DistanceKm(bLat, bLng, uLat, uLng), 1));
        }
    }

    [Fact]
    public void Place_seed_has_unique_osm_ids_and_regional_coordinates()
    {
        var places = PlaceSeedData.Create();

        Assert.True(places.Count >= 50);
        Assert.Equal(places.Count, places.Select(place => place.OsmNodeId).Distinct().Count());
        Assert.All(places, place =>
        {
            Assert.InRange(place.Latitude, 19.6, 20.4);
            Assert.InRange(place.Longitude, -99.8, -99.0);
            Assert.Contains(place.Type, new[] { "neighborhood", "city" });
        });
    }
}
