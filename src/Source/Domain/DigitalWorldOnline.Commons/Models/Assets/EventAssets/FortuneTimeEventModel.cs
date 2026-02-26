public sealed class FortuneTimeEventModel
{
    public int Id { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Dia da semana (0 = Domingo, 1 = Segunda, ..., 6 = Sábado)
    /// </summary>
    public int DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public int ItemId { get; set; }
    public int ItemCount { get; set; }

    /// <summary>
    /// Verifica se o evento está ativo neste momento.
    /// </summary>
    public bool IsActiveNow(DateTime? now = null)
    {
        now ??= DateTime.Now;

        if (now < StartDate || now > EndDate)
            return false;

        if ((int)now.Value.DayOfWeek != DayOfWeek)
            return false;

        var todayStart = now.Value.Date.Add(StartTime);
        var todayEnd = now.Value.Date.Add(EndTime);

        return now.Value >= todayStart && now.Value <= todayEnd;
    }

    /// <summary>
    /// Retorna tempo em segundos até o início de hoje.
    /// </summary>
    public int GetSecondsUntilStart(DateTime? now = null)
    {
        now ??= DateTime.Now;
        var todayStart = now.Value.Date.Add(StartTime);
        return (int)Math.Max(0, (todayStart - now.Value).TotalSeconds);
    }

    /// <summary>
    /// Retorna tempo em segundos até o fim de hoje.
    /// </summary>
    public int GetSecondsUntilEnd(DateTime? now = null)
    {
        now ??= DateTime.Now;
        var todayEnd = now.Value.Date.Add(EndTime);
        return (int)Math.Max(0, (todayEnd - now.Value).TotalSeconds);
    }
}
