﻿using System;
using HlslTools.Formatting;
using HlslTools.VisualStudio.Options.ViewModels;
using HlslTools.VisualStudio.Options.Views;

namespace HlslTools.VisualStudio.Options
{
    internal sealed class HlslFormattingSpacingOptionsPage : OptionsPageBase<SpacingOptions>
    {
        protected override OptionsControlBase CreateControl(IServiceProvider serviceProvider)
        {
            return new OptionsPreviewControl(() => new FormattingSpacingViewModel(serviceProvider, Options));
        }
    }
}