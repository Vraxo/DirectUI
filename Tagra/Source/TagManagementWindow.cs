using DirectUI;
using DirectUI.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Tagra;

public static class TagManagementWindow
{
    private static readonly IReadOnlyList<string> ColorPalette = new List<string>
    {
        "#e53935", "#d81b60", "#8e24aa", "#5e35b1", "#3949ab", "#1e88e5",
        "#039be5", "#00acc1", "#00897b", "#43a047", "#7cb342", "#c0ca33",
        "#fdd835", "#ffb300", "#fb8c00", "#f4511e", "#6d4c41", "#757575",
        "#546e7a", "#FFFFFF", "#000000"
    }.AsReadOnly();

    public static void Draw(App app)
    {
        var host = app.Host;
        var innerWidth = 380f; // Modal content width

        UI.BeginVBoxContainer("tag_manage_vbox", new Vector2(10, 10), gap: 10f);

        // If a delete is pending, show confirmation UI instead of the main UI
        if (app.TagIdToDelete.HasValue)
        {
            var tagToDelete = app.AllTags.FirstOrDefault(t => t.Id == app.TagIdToDelete.Value);
            if (tagToDelete != null)
            {
                UI.Text("delete_header", "Confirm Deletion");
                UI.Separator(innerWidth);
                UI.WrappedText("delete_confirm_text", $"Are you sure you want to delete the tag '{tagToDelete.Name}'? This will remove it from {tagToDelete.FileCount} file(s) and cannot be undone.", new Vector2(innerWidth, 0));

                UI.BeginHBoxContainer("delete_confirm_buttons", UI.Context.Layout.GetCurrentPosition(), gap: 10f);
                if (UI.Button("delete_yes", "Yes, Delete"))
                {
                    app.DbManager.DeleteTag(app.TagIdToDelete.Value);
                    app.TagIdToDelete = null; // Clear the delete request
                    app.RefreshAllData(); // Refresh list
                }
                if (UI.Button("delete_no", "Cancel"))
                {
                    app.TagIdToDelete = null; // Clear the delete request
                }
                UI.EndHBoxContainer();
            }
            else
            {
                app.TagIdToDelete = null; // Tag was not found, clear request
            }
        }
        else // Show the main tag management UI
        {
            UI.Text("manage_tags_header", "Manage Tags");
            UI.Separator(innerWidth);

            // --- New Tag Creation ---
            UI.BeginHBoxContainer("new_tag_hbox", UI.Context.Layout.GetCurrentPosition(), gap: 5f);
            UI.InputText("new_tag_input", ref app.NewTagName, new Vector2(innerWidth - 85, 28f), placeholderText: "New Tag Name");
            if (UI.Button("create_tag_btn", "Add Tag", new Vector2(80, 28)))
            {
                if (!string.IsNullOrWhiteSpace(app.NewTagName))
                {
                    app.DbManager.AddTag(app.NewTagName);
                    app.NewTagName = "";
                    app.RefreshAllData();
                }
            }
            UI.EndHBoxContainer();
            // --- End New Tag Creation ---

            UI.Separator(innerWidth);

            // --- Tag List ---
            UI.BeginScrollableRegion("tags_manage_scroll", new Vector2(innerWidth, 250), out var scrollInnerWidth);
            foreach (var tag in app.AllTags)
            {
                UI.BeginVBoxContainer($"tag_vbox_wrapper_{tag.Id}", UI.Context.Layout.GetCurrentPosition(), gap: 2);

                UI.BeginHBoxContainer($"tag_manage_hbox_{tag.Id}", UI.Context.Layout.GetCurrentPosition(), gap: 5);

                // Color Swatch Button
                var color = ParseColorHex(tag.ColorHex);
                var swatchTheme = new ButtonStylePack { Roundness = 1.0f, BorderLength = 0f };
                swatchTheme.Animation = new DirectUI.Animation.AnimationInfo(0.1f);
                swatchTheme.Normal.FillColor = color;
                swatchTheme.Hover.FillColor = color;
                swatchTheme.Hover.Scale = new Vector2(1.1f, 1.1f);
                swatchTheme.Pressed.FillColor = color;
                swatchTheme.Pressed.Scale = new Vector2(0.9f, 0.9f);

                if (UI.Button($"color_swatch_{tag.Id}", "", new Vector2(24, 24), theme: swatchTheme))
                {
                    app.ActiveColorPickerTagId = app.ActiveColorPickerTagId == tag.Id ? null : tag.Id;
                }

                UI.Text($"tag_name_{tag.Id}", $"{tag.Name} ({tag.FileCount})", new Vector2(scrollInnerWidth - 120, 24));

                // Rename button (placeholder)
                if (UI.Button($"rename_tag_btn_{tag.Id}", "Rename", new Vector2(60, 24), disabled: true))
                {
                    // TODO: Implement renaming logic
                }

                if (UI.Button($"delete_tag_btn_{tag.Id}", "X", new Vector2(24, 24)))
                {
                    app.TagIdToDelete = tag.Id;
                }
                UI.EndHBoxContainer();

                // Conditionally draw the color picker below the item
                if (app.ActiveColorPickerTagId == tag.Id)
                {
                    string tempColor = tag.ColorHex;
                    UI.AutoPanel($"color_picker_panel_{tag.Id}", scrollInnerWidth, (innerWidth) =>
                    {
                        if (UI.ColorSelector($"picker_{tag.Id}", ref tempColor, ColorPalette, new Vector2(20, 20), gap: 5f))
                        {
                            app.DbManager.UpdateTagColor(tag.Id, tempColor);
                            app.RefreshAllData();
                            app.ActiveColorPickerTagId = null; // Close picker on selection
                        }
                    });
                }
                UI.EndVBoxContainer();
            }
            UI.EndScrollableRegion();

            UI.Separator(innerWidth);

            if (UI.Button("close_tags_modal_btn", "Close", new Vector2(80, 32)))
            {
                host.ModalWindowService.CloseModalWindow(0);
            }
        }

        UI.EndVBoxContainer();
    }

    private static Color ParseColorHex(string hex)
    {
        if (!string.IsNullOrEmpty(hex) && hex.StartsWith("#") && hex.Length == 7)
        {
            try
            {
                byte r = Convert.ToByte(hex.Substring(1, 2), 16);
                byte g = Convert.ToByte(hex.Substring(3, 2), 16);
                byte b = Convert.ToByte(hex.Substring(5, 2), 16);
                return new Color(r, g, b, 255);
            }
            catch
            {
                return DefaultTheme.Accent;
            }
        }
        return DefaultTheme.Accent;
    }
}