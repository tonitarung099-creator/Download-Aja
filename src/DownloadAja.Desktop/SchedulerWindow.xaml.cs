using System.Windows;

namespace DownloadAja.Desktop;

public partial class SchedulerWindow : Window
{
    public DateTimeOffset? ScheduledAt { get; private set; }

    public SchedulerWindow(DateTimeOffset? currentSchedule)
    {
        InitializeComponent();

        var local = currentSchedule?.LocalDateTime ?? DateTime.Now.AddMinutes(10);
        EnabledBox.IsChecked = currentSchedule.HasValue;
        DateBox.SelectedDate = local.Date;
        TimeBox.Text = local.ToString("HH:mm");
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (EnabledBox.IsChecked != true)
        {
            ScheduledAt = null;
            DialogResult = true;
            return;
        }

        if (!DateBox.SelectedDate.HasValue)
        {
            ValidationText.Text = "Pilih tanggal.";
            return;
        }

        if (!TimeSpan.TryParse(TimeBox.Text.Trim(), out var time) ||
            time < TimeSpan.Zero ||
            time >= TimeSpan.FromDays(1))
        {
            ValidationText.Text = "Jam harus memakai format seperti 21:30.";
            return;
        }

        var localDateTime = DateBox.SelectedDate.Value.Date + time;
        var scheduled = new DateTimeOffset(DateTime.SpecifyKind(localDateTime, DateTimeKind.Local));

        if (scheduled <= DateTimeOffset.Now)
        {
            ValidationText.Text = "Waktu scheduler harus setelah waktu sekarang.";
            return;
        }

        ScheduledAt = scheduled;
        DialogResult = true;
    }
}
