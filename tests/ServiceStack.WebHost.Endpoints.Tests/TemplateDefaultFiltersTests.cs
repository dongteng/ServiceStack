using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceStack.Templates;
using ServiceStack.Text;
using ServiceStack.VirtualPath;

namespace ServiceStack.WebHost.Endpoints.Tests
{
    public class TemplateDefaultFiltersTests
    {
        public TemplatePagesContext CreateContext(Dictionary<string, object> args = null)
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["foo"] = "bar",
                    ["intVal"] = 1,
                    ["doubleVal"] = 2.2
                }
            };
            
            args.Each((key,val) => context.Args[key] = val);
            
            return context;
        }

        [Test]
        public async Task Does_default_filter_raw()
        {
            var context = CreateContext();
            context.VirtualFiles.WriteFile("page.html", "<h1>{{ '<script>' }}</h1>");
            context.VirtualFiles.WriteFile("page-raw.html", "<h1>{{ '<script>' | raw }}</h1>");

            var result = await new PageResult(context.GetPage("page")).RenderToStringAsync();
            Assert.That(result, Is.EqualTo("<h1>&lt;script&gt;</h1>"));

            result = await new PageResult(context.GetPage("page-raw")).RenderToStringAsync();
            Assert.That(result, Is.EqualTo("<h1><script></h1>"));
        }

        [Test]
        public async Task Does_default_filter_json()
        {
            var context = CreateContext();
            context.VirtualFiles.WriteFile("page.html", "var model = {{ model | json }};");

            var result = await new PageResult(context.GetPage("page"))
            {
                Model = new Model
                {
                    Id = 1,
                    Name = "foo",
                }
            }.RenderToStringAsync();

            Assert.That(result, Is.EqualTo("var model = {\"Id\":1,\"Name\":\"foo\"};"));

            result = await new PageResult(context.GetPage("page")).RenderToStringAsync();
            Assert.That(result, Is.EqualTo("var model = null;"));

            context.VirtualFiles.WriteFile("page-null.html", "var nil = {{ null | json }};");
            result = await new PageResult(context.GetPage("page-null")).RenderToStringAsync();
            Assert.That(result, Is.EqualTo("var nil = null;"));
        }

        [Test]
        public async Task Does_default_filter_appSetting()
        {
            var context = CreateContext().Init();
            context.AppSettings.Set("copyright", "&copy; 2008-2017 ServiceStack");
            context.VirtualFiles.WriteFile("page.html", "<footer>{{ 'copyright' | appSetting | raw }}</footer>");

            var result = await new PageResult(context.GetPage("page")).RenderToStringAsync();

            Assert.That(result, Is.EqualTo("<footer>&copy; 2008-2017 ServiceStack</footer>"));
        }

        [Test]
        public async Task Does_default_filter_arithmetic_using_filter()
        {
            var context = CreateContext().Init();
            context.VirtualFiles.WriteFile("page.html", @"
1 + 1 = {{ 1 | add(1) }}
2 x 2 = {{ 2 | mul(2) }} or {{ 2 | multiply(2) }}
3 - 3 = {{ 3 | sub(3) }} or {{ 3 | subtract(3) }}
4 / 4 = {{ 4 | div(4) }} or {{ 4 | divide(4) }}");

            var result = await new PageResult(context.GetPage("page")).RenderToStringAsync();

            Assert.That(result.SanitizeNewLines(), Is.EqualTo(@"
1 + 1 = 2
2 x 2 = 4 or 4
3 - 3 = 0 or 0
4 / 4 = 1 or 1
".SanitizeNewLines()));
        }

        [Test]
        public async Task Does_default_filter_arithmetic_without_filter()
        {
            var context = CreateContext().Init();
            context.VirtualFiles.WriteFile("page.html", @"
1 + 1 = {{ add(1,1) }}
2 x 2 = {{ mul(2,2) }} or {{ multiply(2,2) }}
3 - 3 = {{ sub(3,3) }} or {{ subtract(3,3) }}
4 / 4 = {{ div(4,4) }} or {{ divide(4,4) }}");

            var html = await new PageResult(context.GetPage("page")).RenderToStringAsync();

            Assert.That(html.SanitizeNewLines(), Is.EqualTo(@"
1 + 1 = 2
2 x 2 = 4 or 4
3 - 3 = 0 or 0
4 / 4 = 1 or 1
".SanitizeNewLines()));
        }

        [Test]
        public void Can_incrment_and_decrement()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["ten"] = 10
                }
            }.Init();
            
            Assert.That(new PageResult(context.OneTimePage("{{ 1 | incr }}")).Result, Is.EqualTo("2"));
            Assert.That(new PageResult(context.OneTimePage("{{ ten | incr }}")).Result, Is.EqualTo("11"));
            Assert.That(new PageResult(context.OneTimePage("{{ 1 | incrBy(2) }}")).Result, Is.EqualTo("3"));
            Assert.That(new PageResult(context.OneTimePage("{{ ten | incrBy(2) }}")).Result, Is.EqualTo("12"));
            Assert.That(new PageResult(context.OneTimePage("{{ incr(1) }}")).Result, Is.EqualTo("2"));
            Assert.That(new PageResult(context.OneTimePage("{{ incr(ten) }}")).Result, Is.EqualTo("11"));
            Assert.That(new PageResult(context.OneTimePage("{{ incrBy(ten,2) }}")).Result, Is.EqualTo("12"));
            
            Assert.That(new PageResult(context.OneTimePage("{{ 1 | decr }}")).Result, Is.EqualTo("0"));
            Assert.That(new PageResult(context.OneTimePage("{{ ten | decrBy(2) }}")).Result, Is.EqualTo("8"));
        }

        [Test]
        public void Can_compare_numbers()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["two"] = 2
                }
            }.Init();
            
            Assert.That(new PageResult(context.OneTimePage("{{ 2 | greaterThan(1) }}")).Result, Is.EqualTo("True"));
            Assert.That(new PageResult(context.OneTimePage("{{ two | greaterThan(1) }}")).Result, Is.EqualTo("True"));
            Assert.That(new PageResult(context.OneTimePage("{{ greaterThan(two,1) }}")).Result, Is.EqualTo("True"));
            Assert.That(new PageResult(context.OneTimePage("{{ greaterThan(2,2) }}")).Result, Is.EqualTo("False"));
            Assert.That(new PageResult(context.OneTimePage("{{ greaterThan(two,2) }}")).Result, Is.EqualTo("False"));
            Assert.That(new PageResult(context.OneTimePage("{{ greaterThan(two,two) }}")).Result, Is.EqualTo("False"));
            
            Assert.That(new PageResult(context.OneTimePage("{{ 'two > 1'    | if(gt(two,1)) | raw }}")).Result, Is.EqualTo("two > 1"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'two > 2'    | if(greaterThan(two,2)) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'two > 3'    | if(greaterThan(two,3)) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'two > two'  | if(greaterThan(two,two)) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'two >= two' | if(greaterThanEqual(two,two)) | raw }}")).Result, Is.EqualTo("two >= two"));

            Assert.That(new PageResult(context.OneTimePage("{{ '1 >= 2' | if(greaterThanEqual(1,2)) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ '2 >= 2' | if(greaterThanEqual(2,2)) | raw }}")).Result, Is.EqualTo("2 >= 2"));
            Assert.That(new PageResult(context.OneTimePage("{{ '3 >= 2' | if(greaterThanEqual(3,2)) | raw }}")).Result, Is.EqualTo("3 >= 2"));

            Assert.That(new PageResult(context.OneTimePage("{{ '1 > 2'  | if(greaterThan(1,2)) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ '2 > 2'  | if(greaterThan(2,2)) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ '3 > 2'  | if(greaterThan(3,2)) | raw }}")).Result, Is.EqualTo("3 > 2"));

            Assert.That(new PageResult(context.OneTimePage("{{ '1 <= 2' | if(lessThanEqual(1,2)) | raw }}")).Result, Is.EqualTo("1 <= 2"));
            Assert.That(new PageResult(context.OneTimePage("{{ '2 <= 2' | if(lessThanEqual(2,2)) | raw }}")).Result, Is.EqualTo("2 <= 2"));
            Assert.That(new PageResult(context.OneTimePage("{{ '3 <= 2' | if(lessThanEqual(3,2)) }}")).Result, Is.EqualTo(""));
            
            Assert.That(new PageResult(context.OneTimePage("{{ '1 < 2'  | if(lessThan(1,2)) | raw }}")).Result, Is.EqualTo("1 < 2"));
            Assert.That(new PageResult(context.OneTimePage("{{ '2 < 2'  | if(lessThan(2,2)) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ '3 < 2'  | if(lessThan(3,2)) }}")).Result, Is.EqualTo(""));
            
            Assert.That(new PageResult(context.OneTimePage("{{ '2 >  2' | if(gt(2,2)) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ '2 >= 2' | if(gte(2,2)) | raw }}")).Result, Is.EqualTo("2 >= 2"));
            Assert.That(new PageResult(context.OneTimePage("{{ '2 <= 2' | if(lte(2,2)) | raw }}")).Result, Is.EqualTo("2 <= 2"));
            Assert.That(new PageResult(context.OneTimePage("{{ '2 <  2' | if(lt(2,2)) }}")).Result, Is.EqualTo(""));
            
            Assert.That(new PageResult(context.OneTimePage("{{ '2 == 2' | if(equals(2,2)) }}")).Result, Is.EqualTo("2 == 2"));
            Assert.That(new PageResult(context.OneTimePage("{{ '2 == 2' | if(eq(2,2)) }}")).Result, Is.EqualTo("2 == 2"));
            Assert.That(new PageResult(context.OneTimePage("{{ '2 != 2' | if(notEquals(2,2)) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ '2 != 2' | if(not(2,2)) }}")).Result, Is.EqualTo(""));
        }

        [Test]
        public void Can_compare_strings()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["foo"] = "foo",
                    ["bar"] = "bar",
                }
            }.Init();
            
            Assert.That(new PageResult(context.OneTimePage("{{ 'foo >  \"foo\"' | if(gt(foo,\"foo\")) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'foo >= \"foo\"' | if(gte(foo,\"foo\")) | raw }}")).Result, Is.EqualTo("foo >= \"foo\""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'foo <= \"foo\"' | if(lte(foo,\"foo\")) | raw }}")).Result, Is.EqualTo("foo <= \"foo\""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'foo <  \"foo\"' | if(lt(foo,\"foo\")) }}")).Result, Is.EqualTo(""));
            
            Assert.That(new PageResult(context.OneTimePage("{{ 'bar >  \"foo\"' | if(gt(bar,\"foo\")) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'bar >= \"foo\"' | if(gte(bar,\"foo\")) | raw }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'bar <= \"foo\"' | if(lte(bar,\"foo\")) | raw }}")).Result, Is.EqualTo("bar <= \"foo\""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'bar <  \"foo\"' | if(lt(bar,\"foo\")) | raw }}")).Result, Is.EqualTo("bar <  \"foo\""));
        }

        [Test]
        public void Can_compare_DateTime()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["year2000"] = new DateTime(2000,1,1),
                    ["year2100"] = new DateTime(2100,1,1),
                }
            }.Init();
            
            Assert.That(new PageResult(context.OneTimePage("{{ 'now >  year2000' | if(gt(now,year2000)) | raw }}")).Result, Is.EqualTo("now >  year2000"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'now >= year2000' | if(gte(now,year2000)) | raw }}")).Result, Is.EqualTo("now >= year2000"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'now <= year2000' | if(lte(now,year2000)) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'now <  year2000' | if(lt(now,year2000)) }}")).Result, Is.EqualTo(""));
            
            Assert.That(new PageResult(context.OneTimePage("{{ 'now >  year2100' | if(gt(now,year2100)) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'now >= year2100' | if(gte(now,year2100)) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'now <= year2100' | if(lte(now,year2100)) | raw }}")).Result, Is.EqualTo("now <= year2100"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'now <  year2100' | if(lt(now,year2100)) | raw }}")).Result, Is.EqualTo("now <  year2100"));
            
            Assert.That(new PageResult(context.OneTimePage("{{ '\"2001-01-01\" >  year2100' | if(gt(\"2001-01-01\",year2100)) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ '\"2001-01-01\" >= year2100' | if(gte(\"2001-01-01\",year2100)) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ '\"2001-01-01\" <= year2100' | if(lte(\"2001-01-01\",year2100)) | raw }}")).Result, Is.EqualTo("\"2001-01-01\" <= year2100"));
            Assert.That(new PageResult(context.OneTimePage("{{ '\"2001-01-01\" <  year2100' | if(lt(\"2001-01-01\",year2100)) | raw }}")).Result, Is.EqualTo("\"2001-01-01\" <  year2100"));
        }

        [Test]
        public void Can_use_logical_boolean_operators()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["foo"] = "foo",
                    ["bar"] = "bar",
                    ["year2000"] = new DateTime(2000,1,1),
                    ["year2100"] = new DateTime(2100,1,1),
                    ["contextTrue"] = true,
                    ["contextFalse"] = false,
                }
            }.Init();
            
            Assert.That(new PageResult(context.OneTimePage("{{ 'or(true,true)' | if(or(true,true)) | raw }}")).Result, Is.EqualTo("or(true,true)"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'or(true,false)' | if(or(true,false)) | raw }}")).Result, Is.EqualTo("or(true,false)"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'or(false,false)' | if(or(false,false)) | raw }}")).Result, Is.EqualTo(""));
            
            Assert.That(new PageResult(context.OneTimePage("{{ 'and(true,true)' | if(and(true,true)) | raw }}")).Result, Is.EqualTo("and(true,true)"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'and(true,false)' | if(and(true,false)) | raw }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'and(false,false)' | if(and(false,false)) | raw }}")).Result, Is.EqualTo(""));
            
            Assert.That(new PageResult(context.OneTimePage("{{ 'or(contextTrue,contextTrue)' | if(or(contextTrue,contextTrue)) | raw }}")).Result, Is.EqualTo("or(contextTrue,contextTrue)"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'or(contextTrue,contextFalse)' | if(or(contextTrue,contextFalse)) | raw }}")).Result, Is.EqualTo("or(contextTrue,contextFalse)"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'or(contextFalse,contextFalse)' | if(or(contextFalse,contextFalse)) | raw }}")).Result, Is.EqualTo(""));
            
            Assert.That(new PageResult(context.OneTimePage("{{ 'or(gt(now,year2000),eq(\"foo\",bar))' | if(or(gt(now,year2000),eq(\"foo\",bar))) | raw }}")).Result, 
                Is.EqualTo("or(gt(now,year2000),eq(\"foo\",bar))"));

            Assert.That(new PageResult(context.OneTimePage(@"{{ 'or(gt(now,year2000),eq(""foo"",bar))' | 
            if (
                or (
                    gt ( now, year2000 ),
                    eq ( ""foo"",  bar )
                )
            ) | raw }}")).Result, 
                Is.EqualTo("or(gt(now,year2000),eq(\"foo\",bar))"));

            
            Assert.That(new PageResult(context.OneTimePage(@"{{ 'or(and(gt(now,year2000),eq(""foo"",bar)),and(gt(now,year2000),eq(""foo"",foo)))' | 
            if ( 
                or (
                    and (
                        gt ( now, year2000 ),
                        eq ( ""foo"", bar  )
                    ),
                    and (
                        gt ( now, year2000 ),
                        eq ( ""foo"", foo  )
                    )
                ) 
            ) | raw }}")).Result, 
                Is.EqualTo(@"or(and(gt(now,year2000),eq(""foo"",bar)),and(gt(now,year2000),eq(""foo"",foo)))"));
        }

        [Test]
        public async Task Does_default_filter_arithmetic_chained_filters()
        {
            var context = CreateContext().Init();

            context.VirtualFiles.WriteFile("page-chained.html",
                @"(((1 + 2) * 3) / 4) - 5 = {{ 1 | add(2) | multiply(3) | divide(4) | subtract(5) }}");
            var result = await new PageResult(context.GetPage("page-chained")).RenderToStringAsync();
            Assert.That(result.SanitizeNewLines(), Is.EqualTo(@"(((1 + 2) * 3) / 4) - 5 = -2.75".SanitizeNewLines()));

            context.VirtualFiles.WriteFile("page-ordered.html",
                @"1 + 2 * 3 / 4 - 5 = {{ 1 | add( divide(multiply(2,3), 4) ) | subtract(5) }}");
            result = await new PageResult(context.GetPage("page-ordered")).RenderToStringAsync();
            Assert.That(result.SanitizeNewLines(), Is.EqualTo(@"1 + 2 * 3 / 4 - 5 = -2.5".SanitizeNewLines()));
        }

        [Test]
        public async Task Does_default_filter_currency()
        {
            var context = CreateContext().Init();
            context.Args[TemplateConstants.DefaultCulture] = new CultureInfo("en-US");

            context.VirtualFiles.WriteFile("page-default.html", "Cost: {{ 99.99 | currency }}");
            context.VirtualFiles.WriteFile("page-culture.html", "Cost: {{ 99.99 | currency(culture) | raw }}");

            var result = await new PageResult(context.GetPage("page-default")).RenderToStringAsync();
            Assert.That(result, Is.EqualTo("Cost: $99.99"));

            result = await new PageResult(context.GetPage("page-culture")) {Args = {["culture"] = "en-AU"}}.RenderToStringAsync();
            Assert.That(result, Is.EqualTo("Cost: $99.99"));

            result = await new PageResult(context.GetPage("page-culture")) {Args = {["culture"] = "en-GB"}}.RenderToStringAsync();
            Assert.That(result, Is.EqualTo("Cost: £99.99"));

            result = await new PageResult(context.GetPage("page-culture")) {Args = {["culture"] = "fr-FR"}}.RenderToStringAsync();
            Assert.That(result, Is.EqualTo("Cost: 99,99 €"));
        }

        [Test]
        public async Task Does_default_filter_format()
        {
            var context = CreateContext().Init();
            context.VirtualFiles.WriteFile("page.html", "{{ 3.14159 | format('{0:N2}') }}");
            
            var result = await new PageResult(context.GetPage("page")).RenderToStringAsync();
            Assert.That(result, Is.EqualTo("3.14"));
        }

        [Test]
        public async Task Does_default_filter_dateFormat()
        {
            var context = CreateContext().Init();
            context.VirtualFiles.WriteFile("dateFormat-default.html", "{{ date | dateFormat }}");
            context.VirtualFiles.WriteFile("dateFormat-custom.html", "{{ date | dateFormat(format) }}");
            
            var result = await new PageResult(context.GetPage("dateFormat-default"))
            {
                Args = { ["date"] = new DateTime(2001,01,01,1,1,1,1, DateTimeKind.Utc) }
            }.RenderToStringAsync();
            Assert.That(result, Is.EqualTo("2001-01-01"));

            context.Args[TemplateConstants.DefaultDateFormat] = "dd/MM/yyyy";
            result = await new PageResult(context.GetPage("dateFormat-default"))
            {
                Args = { ["date"] = new DateTime(2001,01,01,1,1,1,1, DateTimeKind.Utc) }
            }.RenderToStringAsync();
            Assert.That(result, Is.EqualTo("01/01/2001"));

            result = await new PageResult(context.GetPage("dateFormat-custom"))
            {
                Args =
                {
                    ["date"] = new DateTime(2001,01,01,1,1,1,1, DateTimeKind.Utc),
                    ["format"] = "dd.MM.yyyy",
                }
            }.RenderToStringAsync();
            Assert.That(result, Is.EqualTo("01.01.2001"));
        }

        [Test]
        public async Task Does_default_filter_dateTimeFormat()
        {
            var context = CreateContext().Init();
            context.VirtualFiles.WriteFile("dateTimeFormat-default.html", "{{ date | dateTimeFormat }}");
            context.VirtualFiles.WriteFile("dateTimeFormat-custom.html", "{{ date | dateFormat(format) }}");
            
            var result = await new PageResult(context.GetPage("dateTimeFormat-default"))
            {
                Args = { ["date"] = new DateTime(2001,01,01,1,1,1,1, DateTimeKind.Utc) }
            }.RenderToStringAsync();
            Assert.That(result, Is.EqualTo("2001-01-01 01:01:01Z"));

            context.Args[TemplateConstants.DefaultDateTimeFormat] = "dd/MM/yyyy hh:mm";
            result = await new PageResult(context.GetPage("dateTimeFormat-default"))
            {
                Args = { ["date"] = new DateTime(2001,01,01,1,1,1,1, DateTimeKind.Utc) }
            }.RenderToStringAsync();
            Assert.That(result, Is.EqualTo("01/01/2001 01:01"));

            result = await new PageResult(context.GetPage("dateTimeFormat-custom"))
            {
                Args =
                {
                    ["date"] = new DateTime(2001,01,01,1,1,1,1, DateTimeKind.Utc),
                    ["format"] = "dd.MM.yyyy hh.mm.ss",
                }
            }.RenderToStringAsync();
            Assert.That(result, Is.EqualTo("01.01.2001 01.01.01"));
        }

        [Test]
        public async Task Does_default_filter_string_filters()
        {
            var context = CreateContext().Init();

            context.VirtualFiles.WriteFile("page-humanize.html", "{{ 'a_varName' | humanize }}");
            var result = await new PageResult(context.GetPage("page-humanize")).RenderToStringAsync();
            Assert.That(result, Is.EqualTo("A Var Name"));

            context.VirtualFiles.WriteFile("page-titleCase.html", "{{ 'war and peace' | titleCase }}");
            result = await new PageResult(context.GetPage("page-titleCase")).RenderToStringAsync();
            Assert.That(result, Is.EqualTo("War And Peace"));

            context.VirtualFiles.WriteFile("page-lower.html", "{{ 'Title Case' | lower }}");
            result = await new PageResult(context.GetPage("page-lower")).RenderToStringAsync();
            Assert.That(result, Is.EqualTo("title case"));

            context.VirtualFiles.WriteFile("page-upper.html", "{{ 'Title Case' | upper }}");
            result = await new PageResult(context.GetPage("page-upper")).RenderToStringAsync();
            Assert.That(result, Is.EqualTo("TITLE CASE"));

            context.VirtualFiles.WriteFile("page-pascalCase.html", "{{ 'camelCase' | pascalCase }}");
            result = await new PageResult(context.GetPage("page-pascalCase")).RenderToStringAsync();
            Assert.That(result, Is.EqualTo("CamelCase"));

            context.VirtualFiles.WriteFile("page-camelCase.html", "{{ 'PascalCase' | camelCase }}");
            result = await new PageResult(context.GetPage("page-camelCase")).RenderToStringAsync();
            Assert.That(result, Is.EqualTo("pascalCase"));

            context.VirtualFiles.WriteFile("page-substring.html", "{{ 'This is a short sentence' | substring(8) }}... {{ 'These three words' | substring(6,5) }}");
            result = await new PageResult(context.GetPage("page-substring")).RenderToStringAsync();
            Assert.That(result, Is.EqualTo("a short sentence... three"));

            context.VirtualFiles.WriteFile("page-pad.html", "<h1>{{ '7' | padLeft(3) }}</h1><h2>{{ 'tired' | padRight(10) }}</h2>");
            result = await new PageResult(context.GetPage("page-pad")).RenderToStringAsync();
            Assert.That(result, Is.EqualTo("<h1>  7</h1><h2>tired     </h2>"));

            context.VirtualFiles.WriteFile("page-padchar.html", "<h1>{{ '7' | padLeft(3,'0') }}</h1><h2>{{ 'tired' | padRight(10,'z') }}</h2>");
            result = await new PageResult(context.GetPage("page-padchar")).RenderToStringAsync();
            Assert.That(result, Is.EqualTo("<h1>007</h1><h2>tiredzzzzz</h2>"));

            context.VirtualFiles.WriteFile("page-repeating.html", "<h1>long time ago{{ ' ...' | repeating(3) }}</h1>");
            result = await new PageResult(context.GetPage("page-repeating")).RenderToStringAsync();
            Assert.That(result, Is.EqualTo("<h1>long time ago ... ... ...</h1>"));
        }

        [Test]
        public void Does_default_filter_with_no_args()
        {
            var context = CreateContext().Init();

            Assert.That(new PageResult(context.OneTimePage("{{ now | dateFormat('yyyy-MM-dd') }}")).Result, Is.EqualTo(DateTime.Now.ToString("yyyy-MM-dd")));
            Assert.That(new PageResult(context.OneTimePage("{{ utcNow | dateFormat('yyyy-MM-dd') }}")).Result, Is.EqualTo(DateTime.UtcNow.ToString("yyyy-MM-dd")));
        }

        [Test]
        public void Can_build_urls_using_filters()
        {
            var context = CreateContext(new Dictionary<string, object>{ {"baseUrl", "http://example.org" }}).Init();

            Assert.That(new PageResult(context.OneTimePage("{{ baseUrl | addQueryString({ id: 1, foo: 'bar' }) | raw }}")).Result, 
                Is.EqualTo("http://example.org?id=1&foo=bar"));

            Assert.That(new PageResult(context.OneTimePage("{{ baseUrl | addQueryString({ id: 1, foo: 'bar' }) | addHashParams({ hash: 'value' }) | raw }}")).Result, 
                Is.EqualTo("http://example.org?id=1&foo=bar#hash=value"));
        }


    }
}