using Moq;
using NodaTime;
using NUnit.Framework;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;
using TempoDB.Exceptions;


namespace TempoDB.Tests
{
    [TestFixture]
    class ReadMultiRollupTest
    {
        private static DateTimeZone zone = DateTimeZone.Utc;
        private static string json = @"{
            ""rollup"":{
                ""fold"":[""max"",""sum""],
                ""period"":""PT1H""
            },
            ""tz"":""UTC"",
            ""data"":[
                {""t"":""2012-01-01T00:00:00.000+00:00"",""v"":{""sum"":23.45,""max"":12.34}}
            ],
            ""series"":{""id"":""id1"",""key"":""key1"",""name"":"""",""tags"":[],""attributes"":{}}
        }";

        private static string json1 = @"{
            ""rollup"":{
                ""fold"":[""max"",""sum""],
                ""period"":""PT1H""
            },
            ""tz"":""UTC"",
            ""data"":[
                {""t"":""2012-03-27T00:00:00.000+00:00"",""v"":{""sum"":23.45,""max"":12.34}},
                {""t"":""2012-03-27T01:00:00.000+00:00"",""v"":{""sum"":34.56,""max"":23.45}}
            ],
            ""series"":{""id"":""id1"",""key"":""key1"",""name"":"""",""tags"":[],""attributes"":{}}
        }";

        private static string json2 = @"{
            ""rollup"":{
                ""fold"":[""max"",""sum""],
                ""period"":""PT1H""
            },
            ""tz"":""UTC"",
            ""data"":[
                {""t"":""2012-03-27T02:00:00.000+00:00"",""v"":{""sum"":45.67,""max"":34.56}}
            ],
            ""series"":{""id"":""id1"",""key"":""key1"",""name"":"""",""tags"":[],""attributes"":{}}
        }";

        private static string jsonTz = @"{
            ""rollup"":{
                ""fold"":[""max"",""sum""],
                ""period"":""PT1H""
            },
            ""tz"":""America/Chicago"",
            ""data"":[
                {""t"":""2012-03-27T00:00:00.000-05:00"",""v"":{""sum"":23.45,""max"":12.34}},
                {""t"":""2012-03-27T01:00:00.000-05:00"",""v"":{""sum"":34.56,""max"":23.45}}
            ],
            ""series"":{""id"":""id1"",""key"":""key1"",""name"":"""",""tags"":[],""attributes"":{}}
        }";

        private static Series series = new Series("key1");
        private static ZonedDateTime start = zone.AtStrictly(new LocalDateTime(2012, 3, 27, 0, 0, 0));
        private static ZonedDateTime end = zone.AtStrictly(new LocalDateTime(2012, 3, 28, 0, 0, 0));
        private static Interval interval = new Interval(start.ToInstant(), end.ToInstant());
        private static MultiRollup rollup = new MultiRollup(Period.FromHours(1), new Fold[] { Fold.Sum, Fold.Max });

        [Test]
        public void SmokeTest()
        {
            var response = TestCommon.GetResponse(200, json1);
            var client = TestCommon.GetClient(response);

            var cursor = client.ReadMultiRollupDataPoints(series, interval, zone, rollup);

            var expected = new List<MultiDataPoint> {
                new MultiDataPoint(zone.AtStrictly(new LocalDateTime(2012, 3, 27, 0, 0, 0)), new Dictionary<string, double> {{"max", 12.34}, {"sum", 23.45}}),
                new MultiDataPoint(zone.AtStrictly(new LocalDateTime(2012, 3, 27, 1, 0, 0)), new Dictionary<string, double> {{"max", 23.45}, {"sum", 34.56}})
            };
            var output = new List<MultiDataPoint>();
            foreach(MultiDataPoint dp in cursor)
            {
                output.Add(dp);
            }

            Assert.AreEqual(expected, output);
        }

        [Test]
        public void SmokeTestTz()
        {
            var zone = DateTimeZoneProviders.Tzdb["America/Chicago"];
            var response = TestCommon.GetResponse(200, jsonTz);
            var client = TestCommon.GetClient(response);

            var cursor = client.ReadMultiRollupDataPoints(series, interval, zone, rollup);

            var expected = new List<MultiDataPoint> {
                new MultiDataPoint(zone.AtStrictly(new LocalDateTime(2012, 3, 27, 0, 0, 0)), new Dictionary<string, double> {{"max", 12.34}, {"sum", 23.45}}),
                new MultiDataPoint(zone.AtStrictly(new LocalDateTime(2012, 3, 27, 1, 0, 0)), new Dictionary<string, double> {{"max", 23.45}, {"sum", 34.56}})
            };
            var output = new List<MultiDataPoint>();
            foreach(MultiDataPoint dp in cursor)
            {
                output.Add(dp);
            }

            Assert.AreEqual(expected, output);
        }

        [Test]
        public void MultipleSegmentSmokeTest()
        {
            var response1 = TestCommon.GetResponse(200, json1);
            response1.Headers.Add(new Parameter {
                Name = "Link",
                Value = "</v1/series/key/key1/data/rollups/segment/&start=2012-03-27T00:02:00.000-05:00&end=2012-03-28&rollup.period=PT1H&rollup.fold=max&rollup.fold=sum>; rel=\"next\""
            });
            var response2 = TestCommon.GetResponse(200, json2);

            var calls = 0;
            RestResponse[] responses = { response1, response2 };
            var mockclient = new Mock<RestClient>();
            mockclient.Setup(cl => cl.Execute(It.IsAny<RestRequest>())).Returns(() => responses[calls]).Callback(() => calls++);

            var client = TestCommon.GetClient(mockclient.Object);
            var cursor = client.ReadMultiRollupDataPoints(series, interval, zone, rollup);

            var expected = new List<MultiDataPoint> {
                new MultiDataPoint(zone.AtStrictly(new LocalDateTime(2012, 3, 27, 0, 0, 0)), new Dictionary<string, double> {{"max", 12.34}, {"sum", 23.45}}),
                new MultiDataPoint(zone.AtStrictly(new LocalDateTime(2012, 3, 27, 1, 0, 0)), new Dictionary<string, double> {{"max", 23.45}, {"sum", 34.56}}),
                new MultiDataPoint(zone.AtStrictly(new LocalDateTime(2012, 3, 27, 2, 0, 0)), new Dictionary<string, double> {{"max", 34.56}, {"sum", 45.67}})
            };
            var output = new List<MultiDataPoint>();
            foreach(MultiDataPoint dp in cursor)
            {
                output.Add(dp);
            }

            Assert.AreEqual(expected, output);
        }

        [Test]
        public void RequestMethod()
        {
            var response = TestCommon.GetResponse(200, json);
            var mockclient = TestCommon.GetMockRestClient(response);
            var client = TestCommon.GetClient(mockclient.Object);

            client.ReadMultiRollupDataPoints(series, interval, zone, rollup);

            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => req.Method == Method.GET)));
        }

        [Test]
        public void RequestUrl()
        {
            var response = TestCommon.GetResponse(200, json);
            var mockclient = TestCommon.GetMockRestClient(response);
            var client = TestCommon.GetClient(mockclient.Object);

            client.ReadMultiRollupDataPoints(series, interval, zone, rollup);

            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => req.Resource == "/{version}/series/key/{key}/data/rollups/segment/")));
        }

        [Test]
        public void RequestParameters()
        {
            var response = TestCommon.GetResponse(200, json);
            var mockclient = TestCommon.GetMockRestClient(response);
            var client = TestCommon.GetClient(mockclient.Object);
            var start = zone.AtStrictly(new LocalDateTime(2012, 1, 1, 0, 0, 0));
            var end = zone.AtStrictly(new LocalDateTime(2012, 1, 2, 0, 0, 0));
            var interval = new Interval(start.ToInstant(), end.ToInstant());

            client.ReadMultiRollupDataPoints(series, interval, zone, rollup);

            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "key", "key1"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "rollup.period", "PT1H"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "rollup.fold", "max"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "rollup.fold", "sum"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "start", "2012-01-01T00:00:00+00:00"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "end", "2012-01-02T00:00:00+00:00"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "tz", "UTC"))));
        }

        [Test]
        public void RequestParametersInterpolation()
        {
            var response = TestCommon.GetResponse(200, json);
            var mockclient = TestCommon.GetMockRestClient(response);
            var client = TestCommon.GetClient(mockclient.Object);
            var start = zone.AtStrictly(new LocalDateTime(2012, 1, 1, 0, 0, 0));
            var end = zone.AtStrictly(new LocalDateTime(2012, 1, 2, 0, 0, 0));
            var interval = new Interval(start.ToInstant(), end.ToInstant());
            var interpolation = new Interpolation(Period.FromMinutes(1), InterpolationFunction.ZOH);

            client.ReadMultiRollupDataPoints(series, interval, zone, rollup, interpolation:interpolation);

            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "key", "key1"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "rollup.period", "PT1H"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "rollup.fold", "max"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "rollup.fold", "sum"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "start", "2012-01-01T00:00:00+00:00"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "end", "2012-01-02T00:00:00+00:00"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "tz", "UTC"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "interpolation.period", "PT1M"))));
            mockclient.Verify(cl => cl.Execute(It.Is<RestRequest>(req => TestCommon.ContainsParameter(req.Parameters, "interpolation.function", "zoh"))));
        }

        [Test]
        [ExpectedException(typeof(TempoDBException))]
        public void Error()
        {
            var response = TestCommon.GetResponse(403, "You are forbidden");
            var client = TestCommon.GetClient(response);

            client.ReadMultiRollupDataPoints(series, interval, zone, rollup);
        }
    }
}
