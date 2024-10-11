using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using minimal_api.Dominio.Interfaces;
using minimal_api.Dominio.Servicos;
using MinimalApi;
using MinimalApi.Dominio.Entidades;
using MinimalApi.Dominio.Enuns;
using MinimalApi.Dominio.Interfaces;
using MinimalApi.Dominio.ModelViews;
using MinimalApi.Dominio.Servicos;
using MinimalApi.DTOs;
using MinimalApi.Infraestrutura.Db;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
        key = Configuration.GetSection("Jwt").GetValue<string>("Key") ?? "Default";
    }

    private string key;
    public IConfiguration Configuration { get; set; }

    public void ConfigureServices(IServiceCollection services)
    {

        services.AddAuthentication(option =>
        {
            option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(option =>
        {
            option.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateIssuer = false,
                ValidateAudience = false,
            };
        });

        services.AddAuthorization();

        services.AddScoped<IAdministradorServico, AdministradorServico>();
        services.AddScoped<IVeiculoServico, VeiculoServico>();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Insira o token JWT aqui"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
        });

        services.AddDbContext<DbContexto>(options =>
            options.UseMySql(
                Configuration.GetConnectionString("MySql"),
                ServerVersion.AutoDetect(Configuration.GetConnectionString("MySql"))
            )
        );

        services.AddCors(options => {
            options.AddDefaultPolicy(
                builder => {
                    builder.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
        });

    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseCors();

        app.UseEndpoints(endpoints =>
        {
            #region Home
            endpoints.MapGet("/", () => Results.Json(new Home())).AllowAnonymous().WithTags("Home");
            #endregion

            #region Administradores
            string GerarTokenJwt(Administrador adm)
            {
                if (string.IsNullOrEmpty(key)) return string.Empty;

                var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
                var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

                var claims = new List<Claim>() {
                    new Claim("Email", adm.Email),
                    new Claim("Perfil", adm.Perfil),
                    new Claim(ClaimTypes.Role, adm.Perfil)
                };

                var token = new JwtSecurityToken(
                    expires: DateTime.Now.AddDays(1),
                    signingCredentials: credentials,
                    claims: claims
                );

                return new JwtSecurityTokenHandler().WriteToken(token);

            }

            endpoints.MapPost("administradores/login", ([FromBody] LoginDTO loginDTO, IAdministradorServico administradorServico) =>
            {
                var adm = administradorServico.Login(loginDTO);
                if (adm != null)
                {
                    string token = GerarTokenJwt(adm);
                    return Results.Ok(new AdministradorLogado
                    {
                        Email = adm.Email,
                        Perfil = adm.Perfil,
                        Token = token
                    });
                }
                else
                {
                    return Results.Unauthorized();
                }
            }).AllowAnonymous().WithTags("Administradores");

            endpoints.MapGet("administradores", ([FromQuery] int? pagina, IAdministradorServico administradorServico) =>
            {
                var adms = new List<AdministradorModelView>();
                var administradores = administradorServico.Todos(pagina);
                foreach (var adm in administradores)
                {
                    adms.Add(new AdministradorModelView
                    {
                        Id = adm.Id,
                        Email = adm.Email,
                        Perfil = (Perfil)Enum.Parse(typeof(Perfil), adm.Perfil)
                    });
                }

                return Results.Ok(adms);
            })
            .RequireAuthorization()
            .RequireAuthorization(new AuthorizeAttribute { Roles = "Adm" })
            .WithTags("Administradores");

            endpoints.MapGet("administradores/{id}", ([FromRoute] int id, IAdministradorServico administradorServico) =>
            {
                var adm = administradorServico.BuscaPorId(id);
                if (adm == null) return Results.NotFound();

                return Results.Ok(new AdministradorModelView
                {
                    Id = adm.Id,
                    Email = adm.Email,
                    Perfil = (Perfil)Enum.Parse(typeof(Perfil), adm.Perfil)
                });
            }).RequireAuthorization()
            .RequireAuthorization(new AuthorizeAttribute { Roles = "Adm" })
            .WithTags("Administradores");

            endpoints.MapPost("administradores", ([FromBody] AdministradorDTO admDTO, IAdministradorServico administradorServico) =>
            {
                var validacao = new ErrosDeValidacao();

                if (string.IsNullOrEmpty(admDTO.Email))
                    validacao.Mensagens.Add("Email não pode ser vazio");
                if (string.IsNullOrEmpty(admDTO.Password))
                    validacao.Mensagens.Add("Senha não pode ser vazio");
                if (admDTO.Perfil == null)
                    validacao.Mensagens.Add("Perfil não pode ser vazio");

                if (validacao.Mensagens.Count > 0)
                    return Results.BadRequest(validacao);

                var adm = new Administrador
                {
                    Email = admDTO.Email,
                    Senha = admDTO.Password,
                    Perfil = admDTO.Perfil.ToString() ?? Perfil.Editor.ToString()
                };
                administradorServico.Incluir(adm);

                return Results.Created($"/administradores/{adm.Id}", new AdministradorModelView
                {
                    Id = adm.Id,
                    Email = adm.Email,
                    Perfil = (Perfil)Enum.Parse(typeof(Perfil), adm.Perfil)
                });
            }).RequireAuthorization()
            .RequireAuthorization(new AuthorizeAttribute { Roles = "Adm" })
            .WithTags("Administradores");
            #endregion

            #region Veiculos
            ErrosDeValidacao validaDTO(VeiculoDTO veiculoDTO)
            {
                var validacao = new ErrosDeValidacao();

                if (string.IsNullOrEmpty(veiculoDTO.Nome))
                    validacao.Mensagens.Add("O nome não pode estar vazio");

                if (string.IsNullOrEmpty(veiculoDTO.Marca))
                    validacao.Mensagens.Add("A marca não pode estar vazia");

                if (veiculoDTO.Ano < 1950)
                    validacao.Mensagens.Add("Ano inválido, aceito veiculos fabricado somente após 1950.");

                return validacao;
            }

            endpoints.MapPost("/veiculos", ([FromBody] VeiculoDTO veiculoDTO, IVeiculoServico veiculoServico) =>
            {
                var validacao = validaDTO(veiculoDTO);
                if (validacao.Mensagens.Count > 0)
                    return Results.BadRequest(validacao);

                var veiculo = new Veiculo
                {
                    Nome = veiculoDTO.Nome,
                    Marca = veiculoDTO.Marca,
                    Ano = veiculoDTO.Ano
                };
                veiculoServico.Incluir(veiculo);

                return Results.Created($"/veiculos/{veiculo.Id}", veiculo);
            })
            .RequireAuthorization()
            .RequireAuthorization(new AuthorizeAttribute { Roles = "Adm,Editor" })
            .WithTags("Veiculos");

            endpoints.MapGet("/veiculos", ([FromQuery] int? pagina, IVeiculoServico veiculoServico) =>
            {
                var veiculos = veiculoServico.Todos(pagina);

                return Results.Ok(veiculos);
            })
            .RequireAuthorization()
            .RequireAuthorization(new AuthorizeAttribute { Roles = "Adm,Editor" })
            .WithTags("Veiculos");


            endpoints.MapGet("/veiculos/{id}", ([FromRoute] int id, IVeiculoServico veiculoServico) =>
            {
                var veiculo = veiculoServico.BuscarPorId(id);
                if (veiculo == null) return Results.NotFound();

                return Results.Ok(veiculo);
            })
            .RequireAuthorization()
            .RequireAuthorization(new AuthorizeAttribute { Roles = "Adm,Editor" })
            .WithTags("Veiculos");

            endpoints.MapPut("/veiculos/{id}", ([FromRoute] int id, [FromBody] VeiculoDTO veiculoDTO, IVeiculoServico veiculoServico) =>
            {
                var veiculo = veiculoServico.BuscarPorId(id);
                if (veiculo == null) return Results.NotFound();

                var validacao = validaDTO(veiculoDTO);
                if (validacao.Mensagens.Count > 0)
                    return Results.BadRequest(validacao);

                veiculo.Nome = veiculoDTO.Nome;
                veiculo.Marca = veiculoDTO.Marca;
                veiculo.Ano = veiculoDTO.Ano;

                veiculoServico.Atualizar(veiculo);

                return Results.Ok(veiculo);
            })
            .RequireAuthorization()
            .RequireAuthorization(new AuthorizeAttribute { Roles = "Adm" })
            .WithTags("Veiculos");

            endpoints.MapDelete("/veiculos/{id}", ([FromRoute] int id, IVeiculoServico veiculoServico) =>
            {
                var veiculo = veiculoServico.BuscarPorId(id);
                if (veiculo == null) return Results.NotFound();

                veiculoServico.Apagar(veiculo);

                return Results.NoContent();
            })
            .RequireAuthorization()
            .RequireAuthorization(new AuthorizeAttribute { Roles = "Adm" })
            .WithTags("Veiculos");
            #endregion
        });
    }
}