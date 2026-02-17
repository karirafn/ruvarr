namespace Ruvarr.Sonarr.Models;

public sealed record class Season(int SeasonNumber, bool Monitored, Statistics Statistics);