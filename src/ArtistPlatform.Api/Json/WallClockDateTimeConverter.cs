using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArtistPlatform.Api.Json;

/// <summary>
/// Serializa DateTime como "wall clock" (sem sufixo de fuso, ex.: 2026-11-05T20:00:00).
/// O sistema trata os horários como hora local do Brasil — o que o artista digita é o que
/// o contratante vê. Na leitura, aceita o padrão ISO (com ou sem fuso).
/// </summary>
public class WallClockDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Unspecified));
}
