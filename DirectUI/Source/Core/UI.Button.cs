using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using D2D = Vortice.Direct2D1;

namespace DirectUI;

public static partial class UI
{
	public static bool Button(string id, ButtonDefinition definition)
	{
		if (!IsContextValid() || definition is null) return false;
		Button buttonInstance = State.GetOrCreateElement<Button>(id);
		buttonInstance.Position = Context.Layout.ApplyLayout(definition.Position);
		ApplyButtonDefinition(buttonInstance, definition);

		bool pushedClip = false;
		if (Context.Layout.IsInLayoutContainer() && Context.Layout.PeekContainer() is GridContainerState grid)
		{
			float clipStartY = grid.CurrentDrawPosition.Y; float gridBottomY = grid.StartPosition.Y + grid.AvailableSize.Y;
			float clipHeight = Math.Max(0f, gridBottomY - clipStartY);
			Rect cellClipRect = new Rect(grid.CurrentDrawPosition.X, clipStartY, Math.Max(0f, grid.CellWidth), clipHeight);
			if (Context.RenderTarget is not null && cellClipRect.Width > 0 && cellClipRect.Height > 0)
			{ Context.RenderTarget.PushAxisAlignedClip(cellClipRect, D2D.AntialiasMode.Aliased); pushedClip = true; }
		}

		bool clicked = buttonInstance.Update(id);
		if (pushedClip && Context.RenderTarget is not null)
		{ Context.RenderTarget.PopAxisAlignedClip(); }

		Context.Layout.AdvanceLayout(buttonInstance.Size);
		return clicked;
	}
}