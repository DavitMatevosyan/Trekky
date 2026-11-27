using Trekky.Modules.GitHub.Domain;

namespace Trekky.Modules.GitHub.Tests;

public sealed class DocPathTests
{
    [Theory]
    [InlineData("README.md")]
    [InlineData("guide/setup.md")]
    [InlineData("adr/0001 modular monolith.md")]
    [InlineData(".github/notes.md")]
    public void A_relative_path_inside_the_folder_is_kept_as_is(string path)
        => Assert.Equal(path, DocPath.Normalize(path));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/README.md")]
    [InlineData("../README.md")]
    [InlineData("guide/../../src/Program.cs")]
    [InlineData("./README.md")]
    [InlineData("guide//setup.md")]
    [InlineData("guide/")]
    [InlineData("guide\\setup.md")]
    [InlineData("guide/\u0000.md")]
    public void Paths_that_could_leave_the_folder_or_are_malformed_are_refused(string? path)
        => Assert.Null(DocPath.Normalize(path));

    [Fact]
    public void A_path_longer_than_the_limit_is_refused()
        => Assert.Null(DocPath.Normalize(new string('a', DocFile.PathMaxLength) + ".md"));

    [Theory]
    [InlineData("a.md", true)]
    [InlineData("a.MD", true)]
    [InlineData("a.markdown", true)]
    [InlineData("a.mdx", false)]
    [InlineData("a.txt", false)]
    [InlineData("md", false)]
    public void Markdown_files_are_md_or_markdown(string path, bool markdown)
        => Assert.Equal(markdown, DocPath.IsMarkdown(path));

    [Theory]
    [InlineData("images/flow.png", "image/png")]
    [InlineData("a.JPG", "image/jpeg")]
    [InlineData("a.jpeg", "image/jpeg")]
    [InlineData("a.gif", "image/gif")]
    [InlineData("a.webp", "image/webp")]
    [InlineData("a.svg", "image/svg+xml")]
    [InlineData("a.html", null)]
    [InlineData("a.md", null)]
    [InlineData("images.png/readme", null)]
    [InlineData("png", null)]
    public void Only_known_image_types_are_served(string path, string? contentType)
        => Assert.Equal(contentType, DocPath.ImageContentType(path));

    [Theory]
    [InlineData("guide/setup.md", "# Setting up\n\nText", "Setting up")]
    [InlineData("guide/setup.md", "Intro\n\n#  Spaced  ##\n", "Spaced")]
    [InlineData("guide/setup.md", "```\n# not a title\n```\n# Real\n", "Real")]
    [InlineData("guide/setup.md", "## Only a subheading\n", "setup")]
    [InlineData("guide/setup.md", "#NoSpace\n", "setup")]
    [InlineData("guide/deploy.markdown", null, "deploy")]
    [InlineData("README.md", "", "README")]
    public void The_title_is_the_first_level_one_heading_or_the_file_name(string path, string? content, string title)
        => Assert.Equal(title, DocPath.TitleOf(path, content));

    [Fact]
    public void A_long_title_is_cut_to_the_limit()
        => Assert.Equal(DocFile.TitleMaxLength, DocPath.TitleOf("a.md", "# " + new string('x', 500)).Length);
}
