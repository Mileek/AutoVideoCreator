using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using Autofac;
using Caliburn.Micro;
using AutoVideoCreator.Application.Interfaces;

namespace AutoVideoCreator.Application
{
    class Bootstrapper: BootstrapperBase
    {
        private IContainer _container;

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            DisplayRootViewForAsync<IMainViewModel>();
        }

        protected override void Configure()
        { 
            //  configure container
            var builder = new ContainerBuilder();

            builder.RegisterModule<Module>();

            _container = builder.Build();
        }

        protected override object GetInstance(Type serviceType, string key)
        {
            return key == null ? _container.Resolve(serviceType) : _container.ResolveKeyed(key, serviceType);
        }

        protected override IEnumerable<object> GetAllInstances(Type serviceType)
        {
            return _container.Resolve(typeof(IEnumerable<>).MakeGenericType(serviceType)) as IEnumerable<object>;
        }

        protected override void BuildUp(object instance)
        {
            _container.InjectProperties(instance);
        }

        protected override IEnumerable<Assembly> SelectAssemblies()
        {
            return new[] { Assembly.GetExecutingAssembly() };
        }
    }
}
