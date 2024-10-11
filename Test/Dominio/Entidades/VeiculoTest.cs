using MinimalApi.Dominio.Entidades;

namespace Test.Dominio.Entidades;

[TestClass]
public class VeiculoTest
{
    [TestMethod]
    public void TestarGetSetPropiedades()
    {
        var adm = new Veiculo();

        adm.Id = 1;
        adm.Nome = "teste";
        adm.Marca = "teste";
        adm.Ano = 1998;

        Assert.AreEqual(1, adm.Id);
        Assert.AreEqual("teste", adm.Nome);
        Assert.AreEqual("teste", adm.Marca);
        Assert.AreEqual(1998, adm.Ano);
    }
}