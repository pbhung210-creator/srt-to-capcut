using SrtCapcut.Core.CapCut;
using Xunit;

namespace SrtCapcut.Tests;

public sealed class IdGeneratorTests
{
    [Fact]
    public void GiuDinhDangGuid()
    {
        string templateId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

        string id1 = IdGenerator.NewId(templateId);
        string id2 = IdGenerator.NewId(templateId);

        Assert.True(Guid.TryParse(id1, out _));
        Assert.NotEqual(templateId, id1);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void GiuDinhDangHex_CungDoDai()
    {
        string templateId = "a1b2c3d4e5f60718293a4b5c6d7e8f90";

        string id = IdGenerator.NewId(templateId);

        Assert.Equal(templateId.Length, id.Length);
        Assert.Matches("^[0-9a-f]+$", id);
    }

    [Fact]
    public void SinhNhieuId_KhongTrung()
    {
        var ids = new HashSet<string>();
        for (int i = 0; i < 500; i++)
            ids.Add(IdGenerator.NewId("11111111-2222-3333-4444-555555555555"));
        Assert.Equal(500, ids.Count);
    }
}
