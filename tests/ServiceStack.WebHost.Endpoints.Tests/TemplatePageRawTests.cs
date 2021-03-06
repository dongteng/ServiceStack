using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.UI;
using NUnit.Framework;
using ServiceStack.Templates;
using ServiceStack.Text;
using ServiceStack.VirtualPath;

#if NETCORE
using Microsoft.Extensions.Primitives;
#endif

namespace ServiceStack.WebHost.Endpoints.Tests
{
    public class TemplatePageRawTests
    {
        [Test]
        public async Task Can_generate_html_template_with_layout_in_memory()
        {
            var context = new TemplatePagesContext();

            context.VirtualFiles.WriteFile("_layout.html", @"
<html>
  <title>{{ title }}</title>
</head>
<body>
  {{ page }}
</body>");

            context.VirtualFiles.WriteFile("page.html", @"<h1>{{ title }}</h1>");

            var page = context.GetPage("page");
            var result = new PageResult(page)
            {
                Args =
                {
                    {"title", "The Title"},
                }
            };

            var html = await result.RenderToStringAsync();
            
            Assert.That(html, Is.EqualTo(@"<html>
  <title>The Title</title>
</head>
<body>
  <h1>The Title</h1>
</body>"));
        }

        [Test]
        public async Task Can_generate_markdown_template_with_layout_in_memory()
        {
            var context = new TemplatePagesContext
            {
                PageFormats =
                {
                    new MarkdownPageFormat()
                }
            };
            
            context.VirtualFiles.WriteFile("_layout.md", @"
# {{ title }}

Brackets in Layout < & > 

{{ page }}");

            context.VirtualFiles.WriteFile("page.md",  @"## {{ title }}");

            var page = context.GetPage("page");
            var result = new PageResult(page)
            {
                Args =
                {
                    {"title", "The Title"},
                },
                ContentType = MimeTypes.Html,
                OutputTransformers = { MarkdownPageFormat.TransformToHtml },
            };

            var html = await result.RenderToStringAsync();
            
            Assert.That(html.SanitizeNewLines(), Is.EqualTo(@"<h1>The Title</h1>
<p>Brackets in Layout &lt; &amp; &gt; </p>
<h2>The Title</h2>".SanitizeNewLines()));
            
        }

        [Test]
        public async Task Can_generate_markdown_template_with_html_layout_in_memory()
        {
            var context = new TemplatePagesContext
            {
                PageFormats =
                {
                    new MarkdownPageFormat()
                }
            };
            
            context.VirtualFiles.WriteFile("_layout.html", @"
<html>
  <title>{{ title }}</title>
</head>
<body>
  {{ page }}
</body>");

            context.VirtualFiles.WriteFile("page.md",  @"### {{ title }}");

            var page = context.GetPage("page");
            var result = new PageResult(page)
            {
                Args =
                {
                    {"title", "The Title"},
                },
                ContentType = MimeTypes.Html,
                PageTransformers = { MarkdownPageFormat.TransformToHtml },
            };

            var html = await result.RenderToStringAsync();
            
            Assert.That(html.SanitizeNewLines(), Is.EqualTo(@"<html>
  <title>The Title</title>
</head>
<body>
  <h3>The Title</h3>

</body>".SanitizeNewLines()));
        }

        [Test]
        public async Task Does_explode_Model_properties_into_scope()
        {
            var context = new TemplatePagesContext();
            
            context.VirtualFiles.WriteFile("page.html", @"Id: {{ Id }}, Name: {{ Name }}");
            
            var result = await new PageResult(context.GetPage("page"))
            {
                Model = new Model { Id = 1, Name = "<foo>" }
            }.RenderToStringAsync();
            
            Assert.That(result, Is.EqualTo("Id: 1, Name: &lt;foo&gt;"));
        }

        [Test]
        public async Task Does_explode_Model_properties_of_anon_object_into_scope()
        {
            var context = new TemplatePagesContext();
            
            context.VirtualFiles.WriteFile("page.html", @"Id: {{ Id }}, Name: {{ Name }}");
            
            var result = await new PageResult(context.GetPage("page"))
            {
                Model = new { Id = 1, Name = "<foo>" }
            }.RenderToStringAsync();
            
            Assert.That(result, Is.EqualTo("Id: 1, Name: &lt;foo&gt;"));
        }

        [Test]
        public async Task Does_reload_modified_page_contents_in_DebugMode()
        {
            var context = new TemplatePagesContext
            {
                DebugMode = true, //default
            };
            
            context.VirtualFiles.WriteFile("page.html", "<h1>Original</h1>");
            Assert.That(await new PageResult(context.GetPage("page")).RenderToStringAsync(), Is.EqualTo("<h1>Original</h1>"));

            await Task.Delay(1); //Memory VFS is too fast!
            
            context.VirtualFiles.WriteFile("page.html", "<h1>Updated</h1>");
            Assert.That(await new PageResult(context.GetPage("page")).RenderToStringAsync(), Is.EqualTo("<h1>Updated</h1>"));
        }

        [Test]
        public void Context_Throws_FileNotFoundException_when_page_does_not_exist()
        {
            var context = new TemplatePagesContext();

            Assert.That(context.Pages.GetPage("not-exists.html"), Is.Null);

            try
            {
                var page = context.GetPage("not-exists.html");
                Assert.Fail("Should throw");
            }
            catch (FileNotFoundException e)
            {
                e.ToString().Print();
            }
        }

        class MyFilter : TemplateFilter
        {
            public string echo(string text) => $"{text} {text}";
            public string greetArg(string key) => $"Hello {Context.Args[key]}";
        }

        [Test]
        public async Task Does_use_custom_filter()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["contextArg"] = "foo"
                },                
            }.Init();
            
            context.VirtualFiles.WriteFile("page.html", "<h1>{{ 'hello' | echo }}</h1>");
            var result = await new PageResult(context.GetPage("page"))
            {
                TemplateFilters = { new MyFilter() }
            }.RenderToStringAsync();
            Assert.That(result, Is.EqualTo("<h1>hello hello</h1>"));

            context.VirtualFiles.WriteFile("page-greet.html", "<h1>{{ 'contextArg' | greetArg }}</h1>");
            result = await new PageResult(context.GetPage("page-greet"))
            {
                TemplateFilters = { new MyFilter() }
            }.RenderToStringAsync();
            Assert.That(result, Is.EqualTo("<h1>Hello foo</h1>"));
        }

        [Test]
        public async Task Does_embed_partials()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["copyright"] = "Copyright &copy; ServiceStack 2008-2017",
                    ["footer"] = "global-footer"
                }
            }.Init();
            
            context.VirtualFiles.WriteFile("_layout.html", @"
<html>
<head><title>{{ title }}</title></head>
<body>
{{ 'header' | partial }}
<div id='content'>{{ page }}</div>
{{ footer | partial }}
</body>
</html>
");
            context.VirtualFiles.WriteFile("header.html", "<header>{{ pageTitle | titleCase }}</header>");
            context.VirtualFiles.WriteFile("page.html", "<h2>{{ contentTitle }}</h2><section>{{ 'page-content' | partial }}</section>");
            context.VirtualFiles.WriteFile("page-content.html", "<p>{{ contentBody | padRight(20,'.') }}</p>");
            context.VirtualFiles.WriteFile("global-footer.html", "<footer>{{ copyright | raw }}</footer>");
            
            var result = await new PageResult(context.GetPage("page"))
            {
                Args =
                {
                    ["pageTitle"] = "I'm in your header",
                    ["contentTitle"] = "Content is King!",
                    ["contentBody"] = "About this page",
                }
            }.RenderToStringAsync();
            
            Assert.That(result.SanitizeNewLines(), Is.EqualTo(@"
<html>
<head><title>{{ title }}</title></head>
<body>
<header>I&#39;m In Your Header</header>
<div id='content'><h2>Content is King!</h2><section><p>About this page.....</p></section></div>
<footer>Copyright &copy; ServiceStack 2008-2017</footer>
</body>
</html>
".SanitizeNewLines()));
        }

        public class ModelBinding
        {
            public int Int { get; set;  }
            
            public string Prop { get; set; }
            
            public NestedModelBinding Object { get; set; }
            
            public Dictionary<string, ModelBinding> Dictionary { get; set; }
            
            public List<ModelBinding> List { get; set; }
            
            public ModelBinding this[int i]
            {
                get => List[i];
                set => List[i] = value;
            }
        }
        
        public class NestedModelBinding
        {
            public int Int { get; set;  }
            
            public string Prop { get; set; }
            
            public ModelBinding Object { get; set; }
            
            public AltNested AltNested { get; set; }
            
            public Dictionary<string, ModelBinding> Dictionary { get; set; }
            
            public List<ModelBinding> List { get; set; }
        }
        
        public class AltNested
        {
            public string Field { get; set; }
        }


        private static ModelBinding CreateModelBinding()
        {
            var model = new ModelBinding
            {
                Int = 1,
                Prop = "The Prop",
                Object = new NestedModelBinding
                {
                    Int = 2,
                    Prop = "Nested Prop",
                    Object = new ModelBinding
                    {
                        Int = 21,
                        Prop = "Nested Nested Prop",
                    },
                    AltNested = new AltNested
                    {
                        Field = "Object AltNested Field"
                    }
                },
                Dictionary = new Dictionary<string, ModelBinding>
                {
                    {
                        "map-key",
                        new ModelBinding
                        {
                            Int = 3,
                            Prop = "Dictionary Prop",
                            Object = new NestedModelBinding
                            {
                                Int = 5,
                                Prop = "Nested Dictionary Prop",
                                AltNested = new AltNested
                                {
                                    Field = "Dictionary AltNested Field"
                                }
                            }
                        }
                    },
                },
                List = new List<ModelBinding>
                {
                    new ModelBinding
                    {
                        Int = 4,
                        Prop = "List Prop",
                        Object = new NestedModelBinding {Int = 5, Prop = "Nested List Prop"}
                    }
                }
            };
            return model;
        }

        [Test]
        public async Task Does_evaluate_variable_binding_expressions()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["key"] = "the-key",
                }
            }.Init();
            
            context.VirtualFiles.WriteFile("page.html", @"Prop = {{ Prop }}");

            var model = CreateModelBinding();

            var pageResultArg = new NestedModelBinding
            {
                Int = 2,
                Prop = "Nested Prop",
                Object = new ModelBinding
                {
                    Int = 21,
                    Prop = "Nested Nested Prop",
                },
                AltNested = new AltNested
                {
                    Field = "Object AltNested Field"
                }
            };
            
            var result = await new PageResult(context.GetPage("page"))
            {
                Model = model,
                Args = { ["pageResultArg"] = pageResultArg }
            }.Init();

            object value;

            value = result.EvaluateBinding("key");
            Assert.That(value, Is.EqualTo("the-key"));
            value = result.EvaluateBinding("Prop");
            Assert.That(value, Is.EqualTo(model.Prop));

            value = result.EvaluateBinding("model.Prop");
            Assert.That(value, Is.EqualTo(model.Prop));
            value = result.EvaluateBinding("model.Object.Prop");
            Assert.That(value, Is.EqualTo(model.Object.Prop));
            value = result.EvaluateBinding("model.Object.Object.Prop");
            Assert.That(value, Is.EqualTo(model.Object.Object.Prop));
            value = result.EvaluateBinding("model.Object.AltNested.Field");
            Assert.That(value, Is.EqualTo(model.Object.AltNested.Field));
            value = result.EvaluateBinding("model[0].Prop");
            Assert.That(value, Is.EqualTo(model[0].Prop));
            value = result.EvaluateBinding("model[0].Object.Prop");
            Assert.That(value, Is.EqualTo(model[0].Object.Prop));
            value = result.EvaluateBinding("model.List[0]");
            Assert.That(value, Is.EqualTo(model.List[0]));
            value = result.EvaluateBinding("model.List[0].Prop");
            Assert.That(value, Is.EqualTo(model.List[0].Prop));
            value = result.EvaluateBinding("model.List[0].Object.Prop");
            Assert.That(value, Is.EqualTo(model.List[0].Object.Prop));
            value = result.EvaluateBinding("model.Dictionary[\"map-key\"].Prop");
            Assert.That(value, Is.EqualTo(model.Dictionary["map-key"].Prop));
            value = result.EvaluateBinding("model.Dictionary['map-key'].Object.Prop");
            Assert.That(value, Is.EqualTo(model.Dictionary["map-key"].Object.Prop));
            value = result.EvaluateBinding("model.Dictionary['map-key'].Object.AltNested.Field");
            Assert.That(value, Is.EqualTo(model.Dictionary["map-key"].Object.AltNested.Field));
            value = result.EvaluateBinding("Object.AltNested.Field");
            Assert.That(value, Is.EqualTo(model.Object.AltNested.Field));
            
            value = result.EvaluateBinding("pageResultArg.Object.Prop");
            Assert.That(value, Is.EqualTo(pageResultArg.Object.Prop));
            value = result.EvaluateBinding("pageResultArg.AltNested.Field");
            Assert.That(value, Is.EqualTo(pageResultArg.AltNested.Field));
        }

        [Test]
        public async Task Does_evaluate_variable_binding_expressions_in_template()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["key"] = "the-key",
                }
            }.Init();
            
            context.VirtualFiles.WriteFile("page.html", @"
Object.Object.Prop = '{{ Object.Object.Prop }}'
model.Object.Object.Prop = '{{ model.Object.Object.Prop }}'
model.Dictionary['map-key'].Object.AltNested.Field = '{{ model.Dictionary['map-key'].Object.AltNested.Field }}'
model.Dictionary['map-key'].Object.AltNested.Field | lower = '{{ model.Dictionary['map-key'].Object.AltNested.Field | lower }}'
");

            var model = CreateModelBinding();
            
            var result = await new PageResult(context.GetPage("page")) { Model = model }.RenderToStringAsync();
            
            Assert.That(result.SanitizeNewLines(), Is.EqualTo(@"
Object.Object.Prop = 'Nested Nested Prop'
model.Object.Object.Prop = 'Nested Nested Prop'
model.Dictionary['map-key'].Object.AltNested.Field = 'Dictionary AltNested Field'
model.Dictionary['map-key'].Object.AltNested.Field | lower = 'dictionary altnested field'
".SanitizeNewLines()));
        }

//#if NET45
//        [Test]
//        public void DumpExpr()
//        {
//            Expression<Func<object, object>> fn = (o) => ((ModelBinding)o).Dictionary["map-key"].Prop;
//            GetDebugView(fn).Print();
//        }
//        
//        public static string GetDebugView(Expression exp)
//        {
//            var propertyInfo = typeof(Expression).GetProperty("DebugView", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
//            return propertyInfo.GetValue(exp) as string;
//        }
//#endif

        [Test]
        public void Can_render_onetime_page_and_layout()
        {
            var context = new TemplatePagesContext
            {                
                Args = { ["key"] = "the-key" }
            }.Init();

            var adhocPage = context.Pages.OneTimePage("<h1>{{ key }}</h1>", "html");
            var result = new PageResult(adhocPage) { Model = CreateModelBinding() }.Result;
            Assert.That(result, Is.EqualTo("<h1>the-key</h1>"));
            
            adhocPage = context.Pages.OneTimePage("<h1>{{ model.Dictionary['map-key'].Object.AltNested.Field | lower }}</h1>", "html");
            result = new PageResult(adhocPage) { Model = CreateModelBinding() }.Result;
            Assert.That(result, Is.EqualTo("<h1>dictionary altnested field</h1>"));
            
            adhocPage = context.Pages.OneTimePage("<h1>{{ key }}</h1>", "html");
            result = new PageResult(adhocPage)
            {
                LayoutPage = context.Pages.OneTimePage("<html><title>{{ model.List[0].Object.Prop | lower }}</title><body>{{ page }}</body></html>", "html"),
                Model = CreateModelBinding()
            }.Result;
            Assert.That(result, Is.EqualTo("<html><title>nested list prop</title><body><h1>the-key</h1></body></html>"));
        }

        [Test]
        public async Task Can_render_onetime_page_with_real_layout()
        {
            var context = new TemplatePagesContext
            {                
                Args = { ["key"] = "the-key" }
            }.Init();
            
            context.VirtualFiles.WriteFile("_layout.html", "<html><title>{{ model.List[0].Object.Prop | lower }}</title><body>{{ page }}</body></html>");

            var adhocPage = context.Pages.OneTimePage(@"<h1>{{ key }}</h1>", "html");
            var result = await new PageResult(adhocPage)
            {
                LayoutPage = context.GetPage("_layout"),
                Model = CreateModelBinding()
            }.RenderToStringAsync();
            Assert.That(result, Is.EqualTo("<html><title>nested list prop</title><body><h1>the-key</h1></body></html>"));
        }

        public class ModelWithMethods
        {
            public string Name { get; set; }

            public string GetName() => Name;
            
            public ModelWithMethods Nested { get; set; }
        }

        [Test]
        public void Does_not_allow_invoking_method_on_binding_expression()
        {
            var context = new TemplatePagesContext().Init();

            var model = new ModelWithMethods { Nested = new ModelWithMethods { Name = "Nested" } };
            
            try
            {
                var r = new PageResult(context.OneTimePage("{{ model.GetName() }}")){ Model = model }.Result;
                Assert.Fail("Should throw");
            }
            catch (BindingExpressionException e)
            {
                e.Message.Print();
            }

            try
            {
                var r = new PageResult(context.OneTimePage("{{ model.Nested.GetName() }}")){ Model = model }.Result;
                Assert.Fail("Should throw");
            }
            catch (BindingExpressionException e)
            {
                e.Message.Print();
            }
        }

        [Test]
        public void Binding_expressions_with_null_references_evaluate_to_null()
        {
            var context = new TemplatePagesContext().Init();

            Assert.That(new PageResult(context.OneTimePage("{{ model.Object.Prop }}")) { Model = new ModelBinding() }.Result, Is.Empty);
            Assert.That(new PageResult(context.OneTimePage("{{ Object.Prop }}")) { Model = new ModelBinding() }.Result, Is.Empty);
            Assert.That(new PageResult(context.OneTimePage("{{ model.Object.Object.Prop }}")) { Model = new ModelBinding() }.Result, Is.Empty);
            Assert.That(new PageResult(context.OneTimePage("{{ model[0].Prop }}")) { Model = new ModelBinding() }.Result, Is.Empty);
            Assert.That(new PageResult(context.OneTimePage("{{ model.List[0].Prop }}")) { Model = new ModelBinding() }.Result, Is.Empty);
            Assert.That(new PageResult(context.OneTimePage("{{ model.Dictionary['key'].Prop }}")) { Model = new ModelBinding() }.Result, Is.Empty);
        }

        [Test]
        public void when_only_shows_code_when_true()
        {
            var context = new TemplatePagesContext().Init();

            Assert.That(new PageResult(context.OneTimePage("{{ 'Is Authenticated' | when(auth) }}"))
            {
                Args = {["auth"] = true }
            }.Result, Is.EqualTo("Is Authenticated"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'Is Authenticated' | when(auth) }}"))
            {
                Args = {["auth"] = (bool?)true }
            }.Result, Is.EqualTo("Is Authenticated"));

            Assert.That(new PageResult(context.OneTimePage("{{ 'Is Authenticated' | when(auth) }}"))
            {
                Args = {["auth"] = null}
            }.Result, Is.Empty);
            Assert.That(new PageResult(context.OneTimePage("{{ 'Is Authenticated' | when(auth) }}"))
            {
                Args = {["auth"] = false}
            }.Result, Is.Empty);
            Assert.That(new PageResult(context.OneTimePage("{{ 'Is Authenticated' | when(auth) }}"))
            {
                Args = {["auth"] = new AuthUserSession().IsAuthenticated}
            }.Result, Is.Empty);
            Assert.That(new PageResult(context.OneTimePage("{{ 'Is Authenticated' | when(auth) }}")).Result, Is.Empty);
        }

        [Test]
        public void unless_shows_code_when_not_true()
        {
            var context = new TemplatePagesContext().Init();

            Assert.That(new PageResult(context.OneTimePage("{{ 'Not Authenticated' | unless(auth) }}"))
            {
                Args = {["auth"] = true }
            }.Result, Is.Empty);
            Assert.That(new PageResult(context.OneTimePage("{{ 'Not Authenticated' | unless(auth) }}"))
            {
                Args = {["auth"] = (bool?)true }
            }.Result, Is.Empty);

            Assert.That(new PageResult(context.OneTimePage("{{ 'Not Authenticated' | unless(auth) }}"))
            {
                Args = {["auth"] = null}
            }.Result, Is.EqualTo("Not Authenticated"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'Not Authenticated' | unless(auth) }}"))
            {
                Args = {["auth"] = false}
            }.Result, Is.EqualTo("Not Authenticated"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'Not Authenticated' | unless(auth) }}"))
            {
                Args = {["auth"] = new AuthUserSession().IsAuthenticated}
            }.Result, Is.EqualTo("Not Authenticated"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'Not Authenticated' | unless(auth) }}")) {                
            }.Result, Is.EqualTo("Not Authenticated"));
        }

        [Test]
        public void can_use_if_and_ifNot_as_alias_to_when_and_unless()
        {
            var context = new TemplatePagesContext().Init();

            Assert.That(new PageResult(context.OneTimePage("{{ 'Is Authenticated' | if(auth) }}"))
            {
                Args = {["auth"] = true }
            }.Result, Is.EqualTo("Is Authenticated"));
            
            Assert.That(new PageResult(context.OneTimePage("{{ 'Not Authenticated' | ifNot(auth) }}"))
            {
                Args = {["auth"] = true }
            }.Result, Is.Empty);
        }

        [Test]
        public void Can_use_else_and_otherwise_filter_to_show_alternative_content()
        {
            var context = new TemplatePagesContext().Init();

            Assert.That(new PageResult(context.OneTimePage("{{ 'Not Authenticated' | unless(auth) | otherwise('Is Authenticated') }}"))
            {
                Args = {["auth"] = false }
            }.Result, Is.EqualTo("Not Authenticated"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'Not Authenticated' | unless(auth) | otherwise('Is Authenticated') }}"))
            {
                Args = {["auth"] = true }
            }.Result, Is.EqualTo("Is Authenticated"));
            

            Assert.That(new PageResult(context.OneTimePage("{{ 'Is Authenticated' | if(auth) | otherwise('Not Authenticated') }}"))
            {
                Args = {["auth"] = false }
            }.Result, Is.EqualTo("Not Authenticated"));
            
            Assert.That(new PageResult(context.OneTimePage("{{ 'Not Authenticated' | ifNot(auth) | otherwise('Is Authenticated') }}"))
            {
                Args = {["auth"] = true }
            }.Result, Is.EqualTo("Is Authenticated"));
        }

        [Test]
        public void Returns_original_string_with_unknown_variable()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["serverArg"] = "defined" 
                }
            }.Init();

            Assert.That(new PageResult(context.OneTimePage("{{ undefined }}")).Result, Is.EqualTo("{{ undefined }}"));
            Assert.That(new PageResult(context.OneTimePage("{{ serverArg }}")).Result, Is.EqualTo("defined"));
            Assert.That(new PageResult(context.OneTimePage("{{ serverArg | unknownFilter }}")).Result, Is.EqualTo("{{ serverArg | unknownFilter }}"));
            Assert.That(new PageResult(context.OneTimePage("{{ undefined | titleCase }}")).Result, Is.EqualTo("{{ undefined | titleCase }}"));
            
            Assert.That(new PageResult(context.OneTimePage("{{ '' }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ null }}")).Result, Is.EqualTo(""));
        }

        [Test]
        public void Filters_with_HandleUnknownValueAttribute_handles_unkownn_values()
        {
            var context = new TemplatePagesContext().Init();

            Assert.That(new PageResult(context.OneTimePage("{{ undefined | otherwise('undefined serverArg') }}")).Result, Is.EqualTo("undefined serverArg"));
        }

        [Test]
        public void Handles_truthy_and_falsy_conditions()
        {
            var context = new TemplatePagesContext().Init();
            
            Assert.That(new PageResult(context.OneTimePage("{{ undefined | falsy('undefined value') }}")).Result, Is.EqualTo("undefined value"));
            Assert.That(new PageResult(context.OneTimePage("{{ null      | falsy('null value') }}")).Result, Is.EqualTo("null value"));
            Assert.That(new PageResult(context.OneTimePage("{{ ''        | falsy('empty string') }}")).Result, Is.EqualTo("empty string"));
            Assert.That(new PageResult(context.OneTimePage("{{ false     | falsy('false value') }}")).Result, Is.EqualTo("false value"));
            Assert.That(new PageResult(context.OneTimePage("{{ 0         | falsy('0') }}")).Result, Is.EqualTo("0"));

            Assert.That(new PageResult(context.OneTimePage("{{ true      | falsy('true value') }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ ' '       | falsy('0') }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 1         | falsy('one value') }}")).Result, Is.EqualTo(""));

            Assert.That(new PageResult(context.OneTimePage("{{ undefined | truthy('undefined value') }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ true      | truthy('true value') }}")).Result, Is.EqualTo("true value"));
            Assert.That(new PageResult(context.OneTimePage("{{ ' '       | truthy('whitespace') }}")).Result, Is.EqualTo("whitespace"));
            Assert.That(new PageResult(context.OneTimePage("{{ 1         | truthy('one value') }}")).Result, Is.EqualTo("one value"));

            Assert.That(new PageResult(context.OneTimePage("{{ null      | truthy('null value') }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ ''        | truthy('empty string') }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ false     | truthy('false value') }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 0         | truthy('0') }}")).Result, Is.EqualTo(""));
        }

        [Test]
        public void Handles_ifTruthy_and_ifFalsy_conditions()
        {
            var context = new TemplatePagesContext().Init();
            
            Assert.That(new PageResult(context.OneTimePage("{{ 'undefined value' | ifFalsey(undefined) }}")).Result, Is.EqualTo("undefined value"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'null value'      | ifFalsey(null) }}")).Result, Is.EqualTo("null value"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'empty string'    | ifFalsey('') }}")).Result, Is.EqualTo("empty string"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'false value'     | ifFalsey(false) }}")).Result, Is.EqualTo("false value"));
            Assert.That(new PageResult(context.OneTimePage("{{ 0                 | ifFalsey(0) }}")).Result, Is.EqualTo("0"));

            Assert.That(new PageResult(context.OneTimePage("{{ 'true value'      | ifFalsey(true) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'whitespace'      | ifFalsey(' ') }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'one value'       | ifFalsey(1) }}")).Result, Is.EqualTo(""));
            
            Assert.That(new PageResult(context.OneTimePage("{{ 'undefined value' | ifTruthy(undefined) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'null value'      | ifTruthy(null) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'empty string'    | ifTruthy('') }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'false value'     | ifTruthy(false) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 0                 | ifTruthy(0) }}")).Result, Is.EqualTo(""));

            Assert.That(new PageResult(context.OneTimePage("{{ 'true value'      | ifTruthy(true) }}")).Result, Is.EqualTo("true value"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'whitespace'      | ifTruthy(' ') }}")).Result, Is.EqualTo("whitespace"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'one value'       | ifTruthy(1) }}")).Result, Is.EqualTo("one value"));
        }

        [Test]
        public void Handles_strict_if_and_else_conditions()
        {
            var context = new TemplatePagesContext().Init();

            Assert.That(new PageResult(context.OneTimePage("{{ 'undefined value' | ifNot(undefined) }}")).Result, Is.EqualTo("undefined value"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'null value'      | ifNot(null) }}")).Result, Is.EqualTo("null value"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'empty string'    | ifNot('') }}")).Result, Is.EqualTo("empty string"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'false value'     | ifNot(false) }}")).Result, Is.EqualTo("false value"));
            Assert.That(new PageResult(context.OneTimePage("{{ 0                 | ifNot(0) }}")).Result, Is.EqualTo("0"));

            Assert.That(new PageResult(context.OneTimePage("{{ 'true value'      | ifNot(true) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'whitespace'      | ifNot(' ') }}")).Result, Is.EqualTo("whitespace"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'one value'       | ifNot(1) }}")).Result, Is.EqualTo("one value"));
            
            Assert.That(new PageResult(context.OneTimePage("{{ 'undefined value' | if(undefined) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'null value'      | if(null) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'empty string'    | if('') }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'false value'     | if(false) }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 0                 | if(0) }}")).Result, Is.EqualTo(""));
            
            Assert.That(new PageResult(context.OneTimePage("{{ 'true value'      | if(true) }}")).Result, Is.EqualTo("true value"));
            Assert.That(new PageResult(context.OneTimePage("{{ 'whitespace'      | if(' ') }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ 'one value'       | if(1) }}")).Result, Is.EqualTo(""));
        }

        [Test]
        public void Null_exceptions_render_empty_string()
        {
            var context = new TemplatePagesContext
            {
//                RenderExpressionExceptions = true,
                Args =
                {
                    ["contextModel"] = new ModelBinding()
                }
            }.Init();
            
            Assert.That(new PageResult(context.OneTimePage("{{ contextModel.Object.Prop }}")).Result, Is.EqualTo(""));
            Assert.That(new PageResult(context.OneTimePage("{{ contextModel.Object.Prop | otherwise('there is nothing') }}")).Result, Is.EqualTo("there is nothing"));
        }

        [Test]
        public void Can_pass_variables_into_partials()
        {
            var context = new TemplatePagesContext
            {
                Args = { ["defaultMessage"] = "this is the default message" }
            }.Init();

            context.VirtualFiles.WriteFile("_layout.html", @"
<html>
  <title>{{ title }}</title>
</head>
<body>
{{ 'header' | partial({ id: 'the-page', message: 'in your header' }) }}
{{ page }}
</body>");

            context.VirtualFiles.WriteFile("header.html", @"
<header id='{{ id | otherwise('header') }}'>
  {{ message | otherwise(defaultMessage) }}
</header>");

            context.VirtualFiles.WriteFile("page.html", @"<h1>{{ title }}</h1>");

            var result = new PageResult(context.GetPage("page")) 
            {
                Args = { ["title"] = "The title" }
            }.Result;
            Assert.That(result.SanitizeNewLines(), Is.EqualTo(@"
<html>
  <title>The title</title>
</head>
<body>
<header id='the-page'>
  in your header
</header>
<h1>The title</h1>
</body>
".SanitizeNewLines()));            
        }

        [Test]
        public void Can_load_page_with_partial_and_scoped_variables()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["myPartial"] = "my-partial"
                }
            }.Init();

            context.VirtualFiles.WriteFile("_layout.html", @"
<html>
  <title>{{ title }}</title>
</head>
<body>
{{ 'my-partial' | partial({ title: 'with-partial', tag: 'h2' }) }}
{{ myPartial | partial({ title: 'with-partial-binding', tag: 'h2' }) }}
<footer>{{ title }}</footer>
</body>");
            
            context.VirtualFiles.WriteFile("my-partial.html", "<{{ tag }}>{{ title }}</{{ tag }}>");
            
            var result = new PageResult(context.GetPage("my-partial"))
            {
                Args = { ["title"] = "The title" }
            }.Result;
            Assert.That(result.SanitizeNewLines(), Is.EqualTo(@"
<html>
  <title>The title</title>
</head>
<body>
<h2>with-partial</h2>
<h2>with-partial-binding</h2>
<footer>The title</footer>
</body>
".SanitizeNewLines()));
        }

        [Test]
        public void Can_load_page_with_page_or_partial_with_scoped_variables_containing_bindings()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["myPartial"] = "my-partial",
                    ["headingTag"] = "h2",
                }
            }.Init();

            context.VirtualFiles.WriteFile("_layout.html", @"
<html>
  <title>{{ title }}</title>
</head>
<body>
{{ 'my-partial' | partial({ title: title, tag: headingTag }) }}
{{ myPartial | partial({ title: partialTitle, tag: headingTag }) }}
</body>");
            
            context.VirtualFiles.WriteFile("my-partial.html", "<{{ tag }}>{{ title }}</{{ tag }}>");
            
            var result = new PageResult(context.GetPage("my-partial"))
            {
                Args =
                {
                    ["title"] = "The title",
                    ["partialTitle"] = "Partial Title",
                }
            }.Result;
            Assert.That(result.SanitizeNewLines(), Is.EqualTo(@"
<html>
  <title>The title</title>
</head>
<body>
<h2>The title</h2>
<h2>Partial Title</h2>
</body>
".SanitizeNewLines()));
        }

        [Test]
        public void Does_replace_bindings()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["contextTitle"] = "The title",
                    ["contextPartial"] = "bind-partial",
                    ["contextTag"] = "h2",
                    ["a"] = "foo",
                    ["b"] = "bar",
                }
            }.Init();

            context.VirtualFiles.WriteFile("_layout.html", @"
<html>
  <title>{{ title }}</title>
</head>
<body>
{{ contextPartial | partial({ title: contextTitle, tag: contextTag, items: [a,b] }) }}
{{ page }}
</body>");
            
            context.VirtualFiles.WriteFile("bind-partial.html", @"
<{{ tag }}>{{ title | upper }}</{{ tag }}>
<p>{{ items | join(', ') }}</p>");
            
            context.VirtualFiles.WriteFile("bind-page.html", @"
<section>
{{ pagePartial | partial({ tag: pageTag, items: items }) }}
</section>
");
            
            var result = new PageResult(context.GetPage("bind-page"))
            {
                Args =
                {
                    ["title"] = "Page title",
                    ["pagePartial"] = "bind-partial",
                    ["pageTag"] = "h3",
                    ["items"] = new[] { 1, 2, 3 },
                }
            }.Result;

            Assert.That(result.SanitizeNewLines(), Is.EqualTo(@"
<html>
  <title>Page title</title>
</head>
<body>
<h2>THE TITLE</h2>
<p>foo, bar</p>
<section>
<h3>PAGE TITLE</h3>
<p>1, 2, 3</p>
</section>

</body>
".SanitizeNewLines()));

        }

        [Test]
        public void Can_repeat_templates_using_forEach()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["letters"] = new[]{ "A", "B", "C" },
                    ["numbers"] = new[]{ 1, 2, 3 },
                }
            };
            
            Assert.That(new PageResult(context.OneTimePage("<ul> {{ '<li> {{it}} </li>' | forEach(letters) }} </ul>")).Result,
                Is.EqualTo("<ul> <li> A </li><li> B </li><li> C </li> </ul>"));

            Assert.That(new PageResult(context.OneTimePage("<ul> {{ '<li> {{it}} </li>' | forEach(numbers) }} </ul>")).Result,
                Is.EqualTo("<ul> <li> 1 </li><li> 2 </li><li> 3 </li> </ul>"));
        }

        [Test]
        public void Can_repeat_templates_using_forEach_in_page_and_layouts()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["numbers"] = new[]{ 1, 2, 3 },
                }
            };
            
            context.VirtualFiles.WriteFile("_layout.html", @"
<html>
<body>
<header>
<ul> {{ '<li> {{it}} </li>' | forEach(numbers) }} </ul>
</header>
<section>
{{ page }}
</section>
</body>
</html>
");
            context.VirtualFiles.WriteFile("page.html", "<ul> {{ '<li> {{it}} </li>' | forEach(letters) }} </ul>");
            
            var result = new PageResult(context.GetPage("page"))
            {
                Args =
                {
                    ["letters"] = new[]{ "A", "B", "C" },
                }
            }.Result;
            
            Assert.That(result.SanitizeNewLines(),
                Is.EqualTo(@"
<html>
<body>
<header>
<ul> <li> 1 </li><li> 2 </li><li> 3 </li> </ul>
</header>
<section>
<ul> <li> A </li><li> B </li><li> C </li> </ul>
</section>
</body>
</html>"
.SanitizeNewLines()));
        }

        [Test]
        public void Can_repeat_templates_with_bindings_using_forEach()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["items"] = new[]
                    {
                        new ModelBinding { Object = new NestedModelBinding { Prop = "A" }}, 
                        new ModelBinding { Object = new NestedModelBinding { Prop = "B" }}, 
                        new ModelBinding { Object = new NestedModelBinding { Prop = "C" }}, 
                    },
                }
            };
            
            Assert.That(new PageResult(context.OneTimePage("<ul> {{ '<li> {{ it.Object.Prop }} </li>' | forEach(items) }} </ul>")).Result,
                Is.EqualTo("<ul> <li> A </li><li> B </li><li> C </li> </ul>"));
        }

        [Test]
        public void Can_repeat_templates_with_bindings_and_custom_scope_using_forEach()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["items"] = new[]
                    {
                        new ModelBinding { Object = new NestedModelBinding { Prop = "A" }}, 
                        new ModelBinding { Object = new NestedModelBinding { Prop = "B" }}, 
                        new ModelBinding { Object = new NestedModelBinding { Prop = "C" }}, 
                    },
                }
            };
            
            Assert.That(new PageResult(context.OneTimePage("<ul> {{ '<li> {{ item.Object.Prop }} </li>' | forEach(items, 'item') }} </ul>")).Result,
                Is.EqualTo("<ul> <li> A </li><li> B </li><li> C </li> </ul>"));
        }

    }
    
    public static class TestUtils
    {
        public static string SanitizeNewLines(this string text) => text.Trim().Replace("\r", "");
        
        static readonly Regex whitespace = new Regex(@"\s+", RegexOptions.Compiled);
        public static string RemoveAllWhitespace(this StringSegment text) => whitespace.Replace(text.Value, "");
        public static string RemoveAllWhitespace(this string text) => whitespace.Replace(text, "");
    }
}