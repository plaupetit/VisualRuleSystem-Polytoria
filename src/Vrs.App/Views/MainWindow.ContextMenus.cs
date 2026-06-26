using Avalonia.Controls;
using Avalonia.Input;
using Vrs.App.ViewModels;

namespace Vrs.App.Views;

public partial class MainWindow
{
    private async void SceneHierarchyItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control ||
            control.DataContext is not SceneHierarchyItemViewModel item ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var properties = e.GetCurrentPoint(control).Properties;
        if (properties.IsRightButtonPressed)
        {
            OpenSceneHierarchyContextMenu(control, viewModel, item);
            e.Handled = true;
            return;
        }

        if (properties.IsLeftButtonPressed)
        {
            if (item.IsScriptLike &&
                viewModel.ShouldConfirmGraphLoadReplacement &&
                !await ConfirmLoadGraphReplacementAsync(item.Name).ConfigureAwait(true))
            {
                e.Handled = true;
                return;
            }

            await viewModel.SelectSceneHierarchyItemAsync(item).ConfigureAwait(true);
        }
    }

    private void ProjectFileItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control ||
            control.DataContext is not ProjectFileItemViewModel item ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var properties = e.GetCurrentPoint(control).Properties;
        if (properties.IsRightButtonPressed)
        {
            OpenProjectFileContextMenu(control, viewModel, item);
            e.Handled = true;
            return;
        }

        if (properties.IsLeftButtonPressed)
        {
            viewModel.SelectProjectFileItem(item);
        }
    }

    private void OpenSceneHierarchyContextMenu(Control target, MainWindowViewModel viewModel, SceneHierarchyItemViewModel item)
    {
        var menu = new ContextMenu();
        var savedInstance = viewModel.BuildSavedScriptInstanceDeployPreview(item);
        var fileHeader = DisabledMenuItem($"Saved file: {savedInstance.ProjectRelativePath}");
        ToolTip.SetTip(fileHeader, savedInstance.ProjectFileReady
            ? $"{savedInstance.ProjectFileStatusText} · {savedInstance.CreatorObjectKind}"
            : $"Deploy File first: {savedInstance.ProjectRelativePath}");
        menu.Items.Add(fileHeader);
        menu.Items.Add(DisabledMenuItem($"Instance: {savedInstance.TargetPath}"));

        var deploySaved = MenuItem(
            "Deploy Saved Script Instance Here",
            () => viewModel.DeployCurrentSavedScriptInstanceToSceneItemAsync(item, dryRun: false));
        deploySaved.IsEnabled = savedInstance.CanDeploy;
        ToolTip.SetTip(deploySaved, savedInstance.CanDeploy
            ? "Links the current saved VRS file into this Creator hierarchy target."
            : savedInstance.BlockReason);
        menu.Items.Add(deploySaved);

        var dryRunSaved = MenuItem(
            "Dry Run Saved Instance Here",
            () => viewModel.DeployCurrentSavedScriptInstanceToSceneItemAsync(item, dryRun: true));
        dryRunSaved.IsEnabled = savedInstance.CanDeploy;
        ToolTip.SetTip(dryRunSaved, savedInstance.CanDeploy
            ? "Queues a dry-run link command for the current saved VRS file."
            : savedInstance.BlockReason);
        menu.Items.Add(dryRunSaved);

        menu.Items.Add(new Separator());
        menu.Items.Add(DisabledMenuItem("Script file setup"));
        menu.Items.Add(MenuItem("Set Deploy Target", () => viewModel.SelectSceneHierarchyItemAsync(item)));
        menu.Items.Add(MenuItem("Rename Script Target...", () => ShowScriptRenameDialogAsync(viewModel)));
        menu.Items.Add(MenuItem($"Use \"{item.Name}\" As Script Name", () =>
        {
            viewModel.DraftScriptName = item.Name;
            if (viewModel.ApplyScriptRenameCommand.CanExecute(null))
            {
                viewModel.ApplyScriptRenameCommand.Execute(null);
            }

            return Task.CompletedTask;
        }));

        var load = MenuItem("Load Graph From Script", () => LoadGraphFromSceneScriptWithConfirmationAsync(viewModel, item));
        load.IsEnabled = item.IsScriptLike;
        menu.Items.Add(load);

        menu.Open(target);
    }

    private void OpenProjectFileContextMenu(Control target, MainWindowViewModel viewModel, ProjectFileItemViewModel item)
    {
        var menu = new ContextMenu();
        var load = MenuItem("Load VRS Graph From File", () => LoadGraphFromProjectFileWithConfirmationAsync(viewModel, item));
        load.IsEnabled = viewModel.CanLoadGraphFromProjectFile(item);
        ToolTip.SetTip(load, "Loads a .luau file only when it contains VRS graph metadata.");
        menu.Items.Add(load);
        menu.Open(target);
    }

    private async Task LoadGraphFromSceneScriptWithConfirmationAsync(MainWindowViewModel viewModel, SceneHierarchyItemViewModel item)
    {
        if (viewModel.ShouldConfirmGraphLoadReplacement &&
            !await ConfirmLoadGraphReplacementAsync(item.Name).ConfigureAwait(true))
        {
            await viewModel.LoadGraphFromSceneScriptAsync(item, replaceCurrentGraph: false).ConfigureAwait(true);
            return;
        }

        await viewModel.LoadGraphFromSceneScriptAsync(item).ConfigureAwait(true);
    }

    private async Task LoadGraphFromProjectFileWithConfirmationAsync(MainWindowViewModel viewModel, ProjectFileItemViewModel item)
    {
        if (viewModel.ShouldConfirmGraphLoadReplacement &&
            !await ConfirmLoadGraphReplacementAsync(item.ProjectRelativePath).ConfigureAwait(true))
        {
            await viewModel.LoadGraphFromProjectFileAsync(item, replaceCurrentGraph: false).ConfigureAwait(true);
            return;
        }

        await viewModel.LoadGraphFromProjectFileAsync(item).ConfigureAwait(true);
    }

    private static MenuItem DisabledMenuItem(string header)
    {
        return new MenuItem
        {
            Header = header,
            IsEnabled = false
        };
    }

    private static MenuItem MenuItem(string header, Func<Task> action)
    {
        var item = new MenuItem { Header = header };
        item.Click += async (_, _) => await action().ConfigureAwait(true);
        return item;
    }
}
