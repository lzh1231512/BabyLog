FROM mcr.microsoft.com/dotnet/aspnet:9.0

# Set environment variables for large file uploads
ENV ASPNETCORE_MaxRequestBodySize=9223372036854775807
ENV ASPNETCORE_DISABLE_REQUEST_LIMITS=true
ENV DOTNET_SYSTEM_IO_DISABLECACHING=false
ENV DOTNET_SYSTEM_NET_DISABLEIPV6=false
ENV ASPNETCORE_ENVIRONMENT=Production

WORKDIR /dotnet
EXPOSE 8080

# Copy the published application
COPY ./Release .

# Ensure proper permissions
RUN chmod -R 777 /dotnet

# Start the application
ENTRYPOINT ["dotnet", "BabyLog.dll"]