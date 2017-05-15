﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using ShaderTools.CodeAnalysis.Options;
using ShaderTools.Editor.VisualStudio.Core.Navigation;
using ShaderTools.Editor.VisualStudio.Core.Util.Extensions;
using ShaderTools.VisualStudio.LanguageServices.Classification;
using ShaderTools.VisualStudio.LanguageServices.ErrorList;

namespace ShaderTools.Editor.VisualStudio.Core
{
    internal abstract class LanguagePackageBase : Package
    {
        private readonly Dictionary<IVsCodeWindow, CodeWindowManagerBase> _codeWindowManagers = new Dictionary<IVsCodeWindow, CodeWindowManagerBase>();

        internal IComponentModel ComponentModel => (IComponentModel) GetService(typeof(SComponentModel));

        internal CodeWindowManagerBase GetOrCreateCodeWindowManager(IVsCodeWindow window)
        {
            CodeWindowManagerBase value;
            if (!_codeWindowManagers.TryGetValue(window, out value))
                _codeWindowManagers[window] = value = CreateCodeWindowManager(window);
            return value;
        }

        protected abstract CodeWindowManagerBase CreateCodeWindowManager(IVsCodeWindow window);

        internal LanguageInfoBase LanguageInfo { get; private set; }

        protected override void Initialize()
        {
            base.Initialize();

            // Proffer the service.
            LanguageInfo = CreateLanguageInfo();
            ((IServiceContainer)this).AddService(LanguageInfo.GetType(), LanguageInfo, true);

            // Ensure the options persisters are loaded since we have to fetch options from the shell
            var componentModel = this.AsVsServiceProvider().GetComponentModel();
            componentModel.GetExtensions<IOptionPersister>();

            // TODO: Only need to do this once, not per package.
            componentModel.GetService<ThemeColorFixer>();
            componentModel.GetService<ErrorsTableDataSource>();
        }

        protected abstract LanguageInfoBase CreateLanguageInfo();

        internal TOptionsPage GetDialogPage<TOptionsPage>()
            where TOptionsPage : DialogPage
        {
            return (TOptionsPage)GetDialogPage(typeof(TOptionsPage));
        }

        internal new object GetService(Type serviceType)
        {
            return base.GetService(serviceType);
        }

        protected override void Dispose(bool disposing)
        {
            foreach (var window in _codeWindowManagers.Values)
                window.RemoveAdornments();
            _codeWindowManagers.Clear();

            base.Dispose(disposing);
        }
    }
}