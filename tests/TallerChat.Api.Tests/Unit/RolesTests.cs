namespace TallerChat.Api.Tests.Unit;

/// <summary>Matriz de permisos entre roles: es la regla que protege las cuentas de administración frente a los gestores.</summary>
public class RolesTests
{
    [Theory]
    [InlineData(Roles.Admin, Roles.Client, true)]
    [InlineData(Roles.Admin, Roles.Manager, true)]
    [InlineData(Roles.Admin, Roles.Admin, true)]
    [InlineData(Roles.Manager, Roles.Client, true)]
    [InlineData(Roles.Manager, Roles.Manager, false)]
    [InlineData(Roles.Manager, Roles.Admin, false)]
    [InlineData(Roles.Client, Roles.Client, false)]
    [InlineData(Roles.Client, Roles.Manager, false)]
    [InlineData(Roles.Client, Roles.Admin, false)]
    [InlineData(null, Roles.Client, false)]
    [InlineData(Roles.Manager, null, false)]
    [InlineData("superusuario", Roles.Client, false)]
    public void CanManage(string? actor, string? target, bool expected) =>
        Assert.Equal(expected, Roles.CanManage(actor, target));

    [Fact]
    public void El_personal_es_gestor_y_administrador_pero_no_el_cliente()
    {
        Assert.Contains(Roles.Admin, Roles.Staff);
        Assert.Contains(Roles.Manager, Roles.Staff);
        Assert.DoesNotContain(Roles.Client, Roles.Staff);
        Assert.Equal(3, Roles.All.Length);
    }
}
