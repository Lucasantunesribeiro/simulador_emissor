namespace NFe.Tests;

public class UnitTest1
{
    [Fact]
    public void Soma_DeveRetornarResultadoCorreto()
    {
        // Arrange
        var a = 2;
        var b = 3;

        // Act
        var resultado = a + b;

        // Assert
        Assert.Equal(5, resultado);
    }
}
