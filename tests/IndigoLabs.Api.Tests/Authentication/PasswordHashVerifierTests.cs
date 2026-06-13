using IndigoLabs.Api.Authentication;

namespace IndigoLabs.Api.Tests.Authentication;

public sealed class PasswordHashVerifierTests
{
    [Fact]
    public void Verify_ReturnsTrueForConfiguredPassword()
    {
        var result = PasswordHashVerifier.Verify(
            "labs",
            "GIhjXMkl0JSuYkwxVGVhPQ==",
            "dh0yD7/gTl5e4mNOW/fsTJPgOMsBeWJoG97nTH4Vq+U=",
            210_000);

        Assert.True(result);
    }

    [Fact]
    public void Verify_ReturnsFalseForWrongPassword()
    {
        var result = PasswordHashVerifier.Verify(
            "wrong",
            "GIhjXMkl0JSuYkwxVGVhPQ==",
            "dh0yD7/gTl5e4mNOW/fsTJPgOMsBeWJoG97nTH4Vq+U=",
            210_000);

        Assert.False(result);
    }
}
