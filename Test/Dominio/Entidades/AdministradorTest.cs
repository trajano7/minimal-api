using MinimalApi.Dominio.Entidades;

namespace Test.Dominio.Entidades;

[TestClass]
public class AdministradorTest
{
    [TestMethod]
    public void TestarGetSetPropiedades()
    {
        var adm = new Administrador();

        adm.Id = 1;
        adm.Email = "teste@teste.com";
        adm.Senha = "teste";
        adm.Perfil = "Adm";

        Assert.AreEqual(1, adm.Id);
        Assert.AreEqual("teste@teste.com", adm.Email);
        Assert.AreEqual("teste", adm.Senha);
        Assert.AreEqual("Adm", adm.Perfil);
    }
}