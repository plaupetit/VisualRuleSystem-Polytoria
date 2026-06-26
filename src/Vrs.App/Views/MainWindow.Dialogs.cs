using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Vrs.App.ViewModels;

namespace Vrs.App.Views;

public partial class MainWindow
{
    private async Task<bool> ConfirmLoadGraphReplacementAsync(string sourceLabel)
    {
        var accepted = false;
        Window? dialog = null;
        var loadButton = new Button
        {
            Content = "Load",
            Padding = new Thickness(10, 4),
            MinHeight = 30
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(8, 4),
            MinHeight = 30
        };

        loadButton.Click += (_, _) =>
        {
            accepted = true;
            dialog?.Close();
        };
        cancelButton.Click += (_, _) => dialog?.Close();

        dialog = new Window
        {
            Title = "Load VRS Graph",
            Width = 430,
            Height = 205,
            MinWidth = 430,
            MinHeight = 205,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush("#10161f"),
            Content = new StackPanel
            {
                Spacing = 10,
                Margin = new Thickness(14),
                Children =
                {
                    new TextBlock
                    {
                        Text = "Replace Current Graph?",
                        Foreground = Brushes.White,
                        FontWeight = FontWeight.SemiBold,
                        FontSize = 16
                    },
                    SmallMenuText($"Loading {sourceLabel} will replace the current graph in VRS.", "#c5d1dd"),
                    SmallMenuText("Save the current graph first if you need to keep it.", "#ffd36b"),
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Margin = new Thickness(0, 8, 0, 0),
                        Children =
                        {
                            cancelButton,
                            loadButton
                        }
                    }
                }
            }
        };
        dialog.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                dialog.Close();
                e.Handled = true;
            }
        };

        await dialog.ShowDialog(this).ConfigureAwait(true);
        return accepted;
    }

    private async Task ShowScriptRenameDialogAsync(MainWindowViewModel viewModel)
    {
        var previousDraft = viewModel.DraftScriptName;
        var accepted = false;
        Window? dialog = null;
        var creatorPreview = SmallMenuText(viewModel.ScriptCreatorPreviewText, "#9ed8ff");
        var filePreview = SmallMenuText(viewModel.ScriptFilePreviewText, "#9aa8b5");
        var draftBox = new TextBox
        {
            Text = viewModel.DraftScriptName,
            PlaceholderText = "Script name",
            MinHeight = 30
        };

        void RefreshPreview()
        {
            viewModel.DraftScriptName = draftBox.Text ?? "";
            creatorPreview.Text = viewModel.ScriptCreatorPreviewText;
            filePreview.Text = viewModel.ScriptFilePreviewText;
        }

        void ApplyRename()
        {
            accepted = true;
            dialog?.Close();
        }

        draftBox.TextChanged += (_, _) => RefreshPreview();
        draftBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                ApplyRename();
                e.Handled = true;
            }

            if (e.Key == Key.Escape)
            {
                dialog?.Close();
                e.Handled = true;
            }
        };

        var applyButton = new Button
        {
            Content = "Apply Rename",
            Padding = new Thickness(8, 4),
            MinHeight = 30
        };
        applyButton.Click += (_, _) => ApplyRename();

        var cancelButton = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(8, 4),
            MinHeight = 30
        };
        cancelButton.Click += (_, _) => dialog?.Close();

        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(14),
            Children =
            {
                new TextBlock
                {
                    Text = "Rename Script Target",
                    Foreground = Brushes.White,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 16
                },
                draftBox,
                creatorPreview,
                filePreview,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Margin = new Thickness(0, 8, 0, 0),
                    Children =
                    {
                        cancelButton,
                        applyButton
                    }
                }
            }
        };

        dialog = new Window
        {
            Title = "Rename Script Target",
            Width = 430,
            Height = 220,
            MinWidth = 430,
            MinHeight = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush("#10161f"),
            Content = panel
        };

        await dialog.ShowDialog(this).ConfigureAwait(true);

        if (!accepted)
        {
            viewModel.DraftScriptName = previousDraft;
            return;
        }

        if (viewModel.ApplyScriptRenameCommand.CanExecute(null))
        {
            viewModel.ApplyScriptRenameCommand.Execute(null);
        }
    }

    private static TextBlock SmallMenuText(string text, string foregroundHex)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = Brush(foregroundHex),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
}
