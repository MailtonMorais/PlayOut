using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AtemSharp;
using Microsoft.Extensions.DependencyInjection;

namespace AtemTallyPlay
{
    internal class Program
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(
            byte bVk,
            byte bScan,
            uint dwFlags,
            UIntPtr dwExtraInfo);

        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== ATEM Tally Play (.NET 9) ===");

            string ipAtem;
            while (true)
            {
                Console.Write("Digite o IP do ATEM: ");
                ipAtem = (Console.ReadLine() ?? "").Trim();

                if (IPAddress.TryParse(ipAtem, out IPAddress? endereco)
                    && endereco.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    break;
                }
                Console.WriteLine("IP IPv4 inválido.\n");
            }

            int cameraAlvo;
            while (true)
            {
                Console.Write("Número da câmera que acionará o Play: ");
                string entrada = (Console.ReadLine() ?? "").Trim();

                if (int.TryParse(entrada, out cameraAlvo) && cameraAlvo > 0)
                {
                    break;
                }
                Console.WriteLine("Digite um número inteiro positivo.\n");
            }

            // NOVA PARTE: Escolha da tecla do Play
            byte teclaPlay;
            char letraPlay;
            while (true)
            {
                Console.Write("Digite a letra da tecla que acionará o Play (ex: P): ");
                string entradaTecla = (Console.ReadLine() ?? "").Trim().ToUpper();

                // Valida se o utilizador digitou exatamente 1 caractere e se é uma letra
                if (entradaTecla.Length == 1 && char.IsLetter(entradaTecla[0]))
                {
                    letraPlay = entradaTecla[0];
                    teclaPlay = (byte)letraPlay; // Em C#, o valor da letra maiúscula equivale ao Virtual Key Code nativo
                    break;
                }
                Console.WriteLine("Por favor, digite apenas uma letra válida (A-Z).\n");
            }

            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            await using var serviceProvider = new ServiceCollection()
                .AddAtemSharp()
                .BuildServiceProvider();

            var lib = serviceProvider.GetRequiredService<IAtemSharpLibrary>();
            var switcher = lib.CreateAtemSwitcher(ipAtem);

            try
            {
                Console.WriteLine($"\nConectando ao ATEM {ipAtem}...");
                await switcher.ConnectAsync(cancellationToken: cts.Token);

                Console.WriteLine($"Conectado! Monitorando a câmera {cameraAlvo}. Tecla de Play: '{letraPlay}'");
                Console.WriteLine("Pressione Ctrl+C para sair.\n");

                bool ultimoEstadoNoAr = false;

                while (!cts.Token.IsCancellationRequested)
                {
                    int programaAtual = (int)switcher.State.Video.MixEffects[0].ProgramInput;
                    bool estaNoAr = programaAtual == cameraAlvo;

                    if (estaNoAr && !ultimoEstadoNoAr)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Câmera {cameraAlvo} entrou no Program. Acionando tecla '{letraPlay}'.");
                        PressionarTecla(teclaPlay);
                    }

                    ultimoEstadoNoAr = estaNoAr;
                    await Task.Delay(100, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nEncerrando monitoramento...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nErro: {ex.GetBaseException().Message}");
            }
            finally
            {
                try
                {
                    await switcher.DisconnectAsync();
                }
                catch
                {
                    // Ignora silenciosamente erros no encerramento
                }
            }
        }

        private static void PressionarTecla(byte keyCode)
        {
            keybd_event(keyCode, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            Thread.Sleep(50);
            keybd_event(keyCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}