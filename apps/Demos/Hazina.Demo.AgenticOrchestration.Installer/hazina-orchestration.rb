# Homebrew Formula for Hazina Orchestration
#
# Installation:
#   brew tap martiendejong/hazina
#   brew install hazina-orchestration
#
# Or install directly from URL:
#   brew install https://raw.githubusercontent.com/martiendejong/Hazina/main/apps/Demos/Hazina.Demo.AgenticOrchestration.Installer/hazina-orchestration.rb

class HazinaOrchestration < Formula
  desc "Web-based orchestration interface for managing Claude Code CLI instances"
  homepage "https://github.com/martiendejong/Hazina"
  url "https://github.com/martiendejong/Hazina/releases/download/v1.0.0/hazina-orchestration-osx-x64.tar.gz"
  sha256 "" # Will be computed during release
  version "1.0.0"
  license "MIT"

  # Dependencies
  depends_on :macos

  def install
    # Install binary
    bin.install "HazinaOrchestration"

    # Install supporting files
    prefix.install Dir["*"]

    # Create LaunchAgent plist
    (prefix/"com.hazina.orchestration.plist").write service_plist
  end

  def service_plist
    <<~EOS
      <?xml version="1.0" encoding="UTF-8"?>
      <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
      <plist version="1.0">
      <dict>
          <key>Label</key>
          <string>#{plist_name}</string>

          <key>ProgramArguments</key>
          <array>
              <string>#{opt_bin}/HazinaOrchestration</string>
          </array>

          <key>WorkingDirectory</key>
          <string>#{opt_prefix}</string>

          <key>RunAtLoad</key>
          <true/>

          <key>KeepAlive</key>
          <true/>

          <key>StandardOutPath</key>
          <string>#{var}/log/hazina-orchestration.log</string>

          <key>StandardErrorPath</key>
          <string>#{var}/log/hazina-orchestration-error.log</string>

          <key>EnvironmentVariables</key>
          <dict>
              <key>ASPNETCORE_ENVIRONMENT</key>
              <string>Production</string>
              <key>DOTNET_SYSTEM_GLOBALIZATION_INVARIANT</key>
              <string>false</string>
          </dict>
      </dict>
      </plist>
    EOS
  end

  def post_install
    # Create log directory
    (var/"log").mkpath

    # Create default config if it doesn't exist
    config_file = prefix/"appsettings.Production.json"
    unless config_file.exist?
      config_file.write <<~EOS
        {
          "Logging": {
            "LogLevel": {
              "Default": "Information",
              "Microsoft.AspNetCore": "Warning"
            }
          },
          "Kestrel": {
            "Endpoints": {
              "Https": {
                "Url": "https://localhost:5123",
                "Certificate": {
                  "Path": "certificate.pfx",
                  "Password": ""
                }
              }
            }
          },
          "AllowedHosts": "*"
        }
      EOS
    end

    ohai "Hazina Orchestration installed successfully!"
    ohai ""
    ohai "To start the service:"
    ohai "  brew services start hazina-orchestration"
    ohai ""
    ohai "Or run manually:"
    ohai "  HazinaOrchestration"
    ohai ""
    ohai "Access the web interface at:"
    ohai "  https://localhost:5123"
    ohai ""
    ohai "View logs:"
    ohai "  tail -f #{var}/log/hazina-orchestration.log"
  end

  def caveats
    <<~EOS
      Hazina Orchestration has been installed!

      Configuration:
        #{prefix}/appsettings.Production.json

      Service Management:
        Start:   brew services start hazina-orchestration
        Stop:    brew services stop hazina-orchestration
        Restart: brew services restart hazina-orchestration
        Status:  brew services list | grep hazina

      Manual Launch:
        HazinaOrchestration

      Logs:
        Output:  #{var}/log/hazina-orchestration.log
        Errors:  #{var}/log/hazina-orchestration-error.log

      Web Interface:
        https://localhost:5123

      Note: First run may require accepting a security prompt.
            Go to System Settings > Privacy & Security if blocked.
    EOS
  end

  service do
    run [opt_bin/"HazinaOrchestration"]
    working_dir opt_prefix
    keep_alive true
    log_path var/"log/hazina-orchestration.log"
    error_log_path var/"log/hazina-orchestration-error.log"
    environment_variables ASPNETCORE_ENVIRONMENT: "Production", DOTNET_SYSTEM_GLOBALIZATION_INVARIANT: "false"
  end

  test do
    # Test that the binary exists and is executable
    assert_predicate bin/"HazinaOrchestration", :exist?
    assert_predicate bin/"HazinaOrchestration", :executable?

    # Test version output (requires implementing --version flag in app)
    # system "#{bin}/HazinaOrchestration", "--version"
  end
end
