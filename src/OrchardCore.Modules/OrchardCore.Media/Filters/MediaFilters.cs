using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid;
using Fluid.Values;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Liquid;
using OrchardCore.Media.Services;

namespace OrchardCore.Media.Filters
{
    public class MediaUrlFilter : ILiquidFilter
    {
        private readonly IMediaFileStore _mediaFileStore;

        public MediaUrlFilter(IMediaFileStore mediaFileStore)
        {
            _mediaFileStore = mediaFileStore;
        }

        public ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, TemplateContext ctx)
        {
            var url = input.ToStringValue();
            var imageUrl = _mediaFileStore.MapPathToPublicUrl(url);

            return new ValueTask<FluidValue>(new StringValue(imageUrl ?? url));
        }
    }

    public class ImageTagFilter : ILiquidFilter
    {
        public ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, TemplateContext ctx)
        {
            var url = input.ToStringValue();

            var imgTag = $"<img src=\"{url}\"";

            foreach (var name in arguments.Names)
            {
                imgTag += $" {name.Replace('_', '-')}=\"{arguments[name].ToStringValue()}\"";
            }

            imgTag += " />";

            return new ValueTask<FluidValue>(new StringValue(imgTag) { Encode = false });
        }
    }

    public class ResizeUrlFilter : ILiquidFilter
    {
        public async ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, TemplateContext ctx)
        {
            var url = input.ToStringValue();

            IDictionary<string, string> queryStringParams = null;
            if (!ctx.AmbientValues.TryGetValue("Services", out var services))
            {
                throw new ArgumentException("Services missing while invoking 'resize_url'");
            }

            var serviceProvider = ((IServiceProvider)services);

            // Profile is a named argument only.
            var profile = arguments["profile"];

            if (!profile.IsNil())
            {
                var mediaProfileService = serviceProvider.GetRequiredService<IMediaProfileService>();
                queryStringParams = await mediaProfileService.GetMediaProfileCommands(profile.ToStringValue());

                // Additional commands to a profile must be named.
                var width = arguments["width"];
                var height = arguments["height"];
                var mode = arguments["mode"];
                var quality = arguments["quality"];
                var format = arguments["format"];
                var center = arguments["center"];

                ApplyQueryStringParams(queryStringParams, width, height, mode, quality, format, center);
            }
            else
            {
                queryStringParams = new Dictionary<string, string>();

                var width = arguments["width"].Or(arguments.At(0));
                var height = arguments["height"].Or(arguments.At(1));
                var mode = arguments["mode"].Or(arguments.At(2));
                var quality = arguments["quality"].Or(arguments.At(3));
                var format = arguments["format"].Or(arguments.At(4));
                var center = arguments["center"].Or(arguments.At(5));

                ApplyQueryStringParams(queryStringParams, width, height, mode, quality, format, center);
            }

            var resizedUrl = QueryHelpers.AddQueryString(url, queryStringParams);

            var mediaOptions = serviceProvider.GetRequiredService<IOptions<MediaOptions>>().Value;

            if (mediaOptions.UseTokenizedQueryString)
            {
                var mediaTokenService = serviceProvider.GetRequiredService<IMediaTokenService>();
                resizedUrl = mediaTokenService.TokenizePath(resizedUrl);
            }

            return new StringValue(resizedUrl);
        }

        private static void ApplyQueryStringParams(IDictionary<string, string> queryStringParams, FluidValue width, FluidValue height, FluidValue mode, FluidValue quality, FluidValue format, FluidValue center)
        {
            if (!width.IsNil())
            {
                queryStringParams["width"] = width.ToStringValue();
            }

            if (!height.IsNil())
            {
                queryStringParams["height"] = height.ToStringValue();
            }

            if (!mode.IsNil())
            {
                queryStringParams["rmode"] = mode.ToStringValue();
            }

            if (!quality.IsNil())
            {
                queryStringParams["quality"] = quality.ToStringValue();
            }

            if (!format.IsNil())
            {
                queryStringParams["format"] = format.ToStringValue();
            }

            if (!center.IsNil() && center.Type == FluidValues.Array)
            {
                var xy = String.Empty;
                foreach (var value in center.Enumerate())
                {
                    if (value.IsNil())
                    {
                        xy = String.Empty;
                        break;
                    }
                    xy = xy + value.ToNumberValue().ToString() + ',';
                }
                if (!String.IsNullOrEmpty(xy))
                {
                    queryStringParams["rxy"] = xy.Trim(',');
                }
            }
        }
    }
}
