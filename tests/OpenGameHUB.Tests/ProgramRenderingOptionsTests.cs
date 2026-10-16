namespace OpenGameHUB.Tests;

public sealed class ProgramRenderingOptionsTests
{
    [Fact]
    public void UseLowMemoryComposition_is_true_when_software_rendering_env_set()
    {
        var previousSoftware = Environment.GetEnvironmentVariable("OPENGAMEHUB_RENDER_SOFTWARE");
        var previousLowMemory = Environment.GetEnvironmentVariable("OPENGAMEHUB_LOW_MEMORY");
        try
        {
            Environment.SetEnvironmentVariable("OPENGAMEHUB_RENDER_SOFTWARE", "1");
            Environment.SetEnvironmentVariable("OPENGAMEHUB_LOW_MEMORY", null);

            Assert.True(Program.UseSoftwareRendering);
            Assert.True(Program.UseLowMemoryComposition);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGAMEHUB_RENDER_SOFTWARE", previousSoftware);
            Environment.SetEnvironmentVariable("OPENGAMEHUB_LOW_MEMORY", previousLowMemory);
        }
    }

    [Fact]
    public void UseLowMemoryComposition_is_true_when_low_memory_env_set()
    {
        var previousSoftware = Environment.GetEnvironmentVariable("OPENGAMEHUB_RENDER_SOFTWARE");
        var previousLowMemory = Environment.GetEnvironmentVariable("OPENGAMEHUB_LOW_MEMORY");
        try
        {
            Environment.SetEnvironmentVariable("OPENGAMEHUB_RENDER_SOFTWARE", null);
            Environment.SetEnvironmentVariable("OPENGAMEHUB_LOW_MEMORY", "1");

            Assert.False(Program.UseSoftwareRendering);
            Assert.True(Program.UseLowMemoryComposition);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENGAMEHUB_RENDER_SOFTWARE", previousSoftware);
            Environment.SetEnvironmentVariable("OPENGAMEHUB_LOW_MEMORY", previousLowMemory);
        }
    }
}
