﻿using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Microsoft.Extensions.DependencyInjection;
using System;
using Kangaroo.UI.ViewModels;

namespace Kangaroo.UI;

public sealed class ViewLocator: IDataTemplate
{

    public Control? Build(object? data)
    {
        if (data == null)
        {
            return null;
        }

        var name = data.GetType().FullName!.Replace("ViewModel", "View");
        var type = Type.GetType(name);

        if (type == null)
        {
            return new TextBlock{Text = "Not Found: " +  name};
        }

        try
        {
            var view = App.Services!.GetService(type);

            if (view is not Control control)
            {
                return new TextBlock { Text = "Invalid Control: " + name };
            }

            return control;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        

    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
