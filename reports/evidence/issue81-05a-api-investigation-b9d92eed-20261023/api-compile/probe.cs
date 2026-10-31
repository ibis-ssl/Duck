using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using System.Linq;
static class ApiUsage { public static IResourceBuilder<ContainerResource> Configure(IResourceBuilder<ContainerResource> builder) { builder.WithHttpEndpoint(targetPort: 8080, name: "http", isProxied: false); builder.Resource.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "http").TargetHost = "127.0.0.1"; return builder; } }
