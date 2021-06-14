# ShiroFlake

 ShiroFlake is a distributed unique ID generator inspired by on [Twitter's Snowflake](https://blog.twitter.com/2010/announcing-snowflake)

 ShiroFlake by default using same parameter that used in [Twitter's Snowflake](https://blog.twitter.com/2010/announcing-snowflake).

 A ShiroFlake ID is composed of

    41 bits for time in units
    12 bits for a machine id
    10 bits for a sequence number

 But you can set the `unsigned` parameter to `true` at initialization the generator to use full 64 bit.
 
```C#
var generator = new ShiroFlakeGenerator(
    1, // machine id
    unsigned: true);
 ```
 
As a result, ShiroFlake has the following advantages:
- The lifetime (~139 years if unsigned is true) is longer than that of Snowflake (69 years)
- It can work in more distributed machines (2^12) than Snowflake (2^10)
