using FluentAssertions;

public class SelectorTests
{
    [Theory]
    [InlineData("test=none", "test", "none")]
    [InlineData(" test = none ", "test", "none")]
    public void ParseTest_WithEqualityOperators(string selector, string key, string value)
    {
        var sel = Selectors.TryParseSelector(selector);
        if(sel is Selector.Equality s){
            s.key.Should().BeEquivalentTo(key);
            s.value.Should().BeEquivalentTo(value);
        } else {
            Assert.Fail("Selector was not of type Equality");
        }
    }
    
    [Theory]
    [InlineData("test!=none", "test", "none")]
    [InlineData(" test != none ", "test", "none")]
    public void ParseTest_WithInEqualityOperators(string selector, string key, string value)
    {
        var sel = Selectors.TryParseSelector(selector);

        if(sel is Selector.Inequality s){
            s.key.Should().BeEquivalentTo(key);
            s.value.Should().BeEquivalentTo(value);
        } else {
            Assert.Fail("Selector was not of type Inequality");
        }
    }
    
    [Theory]
    [InlineData("test", "test")]
    [InlineData(" test ", "test")]
    public void ParseTest_WithExistsOperator(string selector, string key)
    {
        var sel = Selectors.TryParseSelector(selector);

        if(sel is Selector.Exists s){
            s.key.Should().BeEquivalentTo(key);
        } else {
            Assert.Fail("Selector was not of type Exists");
        }
    }
    
    [Theory]
    [InlineData("!test", "test")]
    [InlineData(" !test ", "test")]
    [InlineData(" ! test ", "test")]
    public void ParseTest_WithNotExistsOperator(string selector, string key)
    {
        var sel = Selectors.TryParseSelector(selector);

        if(sel is Selector.NotExists s){
            s.key.Should().BeEquivalentTo(key);
        } else {
            Assert.Fail("Selector was not of type NotExists");
        }
    }
    
    [Theory]
    [InlineData("test in (1,2,3)", "test", new [] {"1", "2", "3"})]
    [InlineData("test in ( 1, 2, 3 3)", "test", new [] {"1", "2", "3 3"})]
    [InlineData("test in (none, any)", "test", new [] {"none", "any"})]
    public void ParseTest_WithInSetOperator(string selector, string key, string[] value)
    {
        var sel = Selectors.TryParseSelector(selector);

        if(sel is Selector.InSet s){
            s.key.Should().BeEquivalentTo(key);
            s.values.Should().BeEquivalentTo(value);
        } else {
            Assert.Fail("Selector was not of type InSet");
        }
    }
    
    [Theory]
    [InlineData("test notin (1,2,3)", "test", new [] {"1", "2", "3"})]
    [InlineData("test notin ( 1, 2, 3 3)", "test", new [] {"1", "2", "3 3"})]
    [InlineData("test notin (none, any)", "test", new [] {"none", "any"})]
    public void ParseTest_WithNotInSetOperator(string selector, string key, string[] value)
    {
        var sel = Selectors.TryParseSelector(selector);

        if(sel is Selector.NotInSet s){
            s.key.Should().BeEquivalentTo(key);
            s.values.Should().BeEquivalentTo(value);
        } else {
            Assert.Fail("Selector was not of type NotInSet");
        }
    }
}
